using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Data;
using QueueQr.Api.Dtos;
using QueueQr.Api.Services;

namespace QueueQr.Api.Controllers;

[ApiController]
[Route("api/public")]
public sealed class PublicController(
    QueueService queue,
    AppDbContext db,
    SiteTokenService siteTokenService,
    CustomerSessionService sessionService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("sites")]
    public async Task<ActionResult<IReadOnlyList<SiteCatalogDto>>> GetSites(CancellationToken cancellationToken)
    {
        var sites = await db.Sites
            .AsNoTracking()
            .Include(s => s.Rooms)
            .OrderBy(s => s.Slug)
            .ToListAsync(cancellationToken);

        return Ok(sites.Select(s => new SiteCatalogDto(
            s.Id,
            s.Slug,
            s.Name,
            s.Rooms
                .OrderBy(r => r.Slug)
                .Select(r => new RoomCatalogDto(r.Id, r.Slug, r.Name, r.ServiceMinutes))
                .ToList()
        )).ToList());
    }

    [HttpPost("customers/login")]
    public async Task<ActionResult<CustomerLoginResponse>> Login(
        [FromBody] CustomerLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest("Phone is required");
        }

        return Ok(await queue.LoginCustomerAsync(request, cancellationToken));
    }

    [HttpGet("sites/{siteSlug}/rooms/{roomSlug}/status")]
    public async Task<ActionResult<RoomStatusResponse>> GetStatus(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        [FromQuery] Guid? ticketId,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.GetRoomStatusAsync(siteSlug, roomSlug, ticketId, cancellationToken));
    }

    [HttpPost("sites/{siteSlug}/rooms/{roomSlug}/tickets")]
    public async Task<ActionResult> TakeTicket(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        [FromBody] TakeTicketRequest request,
        CancellationToken cancellationToken)
    {
        // Validate session token if dynamic QR is enabled
        if (IsDynamicQrEnabled())
        {
            var sessionToken = Request.Headers["X-Session-Token"].FirstOrDefault();
            var session = sessionService.ValidateSession(sessionToken);
            if (session is null)
                return StatusCode(403, new { error = "Phiên làm việc không hợp lệ. Vui lòng scan QR tại cơ sở.", code = "INVALID_SESSION" });
            if (!string.Equals(session.SiteSlug, siteSlug, StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { error = "Phiên làm việc không khớp với cơ sở này.", code = "SITE_MISMATCH" });
        }

        var (ticket, status, myTicket) = await queue.TakeTicketAsync(siteSlug, roomSlug, request, cancellationToken);
        return Ok(new
        {
            ticketId = ticket.Id,
            number = ticket.Number,
            status,
            myTicket,
        });
    }

    /// <summary>
    /// Take tickets for multiple rooms at once.
    /// </summary>
    [HttpPost("sites/{siteSlug}/tickets")]
    public async Task<ActionResult<TakeMultiRoomTicketResponse>> TakeMultiRoomTickets(
        [FromRoute] string siteSlug,
        [FromBody] TakeMultiRoomTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RoomSlugs is null || request.RoomSlugs.Length == 0)
            return BadRequest("Vui lòng chọn ít nhất 1 phòng");

        // Validate session token if dynamic QR is enabled
        if (IsDynamicQrEnabled())
        {
            var sessionToken = request.SessionToken ?? Request.Headers["X-Session-Token"].FirstOrDefault();
            var session = sessionService.ValidateSession(sessionToken);
            if (session is null)
                return StatusCode(403, new { error = "Phiên làm việc không hợp lệ. Vui lòng scan QR tại cơ sở.", code = "INVALID_SESSION" });
            if (!string.Equals(session.SiteSlug, siteSlug, StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { error = "Phiên làm việc không khớp với cơ sở này.", code = "SITE_MISMATCH" });
        }

        var results = new List<MultiRoomTicketResult>();

        foreach (var roomSlug in request.RoomSlugs)
        {
            var takeRequest = new TakeTicketRequest(request.CustomerId);
            var (ticket, _, _) = await queue.TakeTicketAsync(siteSlug, roomSlug, takeRequest, cancellationToken);

            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Slug == roomSlug && r.Site!.Slug == siteSlug, cancellationToken);
            var displayNumber = ShiftCalculator.FormatTicketNumber(ticket.ShiftPrefix, ticket.Number);

            results.Add(new MultiRoomTicketResult(
                ticket.Id,
                roomSlug,
                room?.Name ?? roomSlug,
                ticket.Number,
                displayNumber
            ));
        }

        return Ok(new TakeMultiRoomTicketResponse(results.ToArray()));
    }

    // ── Dynamic QR Token endpoints ────────────────────────────────────

    /// <summary>
    /// Get current QR token for a site (used by TV/display screens).
    /// </summary>
    [HttpGet("sites/{siteSlug}/qr-token")]
    public async Task<ActionResult<SiteQrTokenDto>> GetQrToken(
        [FromRoute] string siteSlug,
        CancellationToken cancellationToken)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == siteSlug, cancellationToken);
        if (site is null) return NotFound("Site not found");

        var token = await siteTokenService.GetOrCreateTokenAsync(site.Id, cancellationToken);

        var frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";
        var qrUrl = $"{frontendUrl}/s/{siteSlug}?token={token.Token}";

        return Ok(new SiteQrTokenDto(token.Token, qrUrl, token.ExpiresAt));
    }

    /// <summary>
    /// Verify a QR token after scanning. Returns a session token on success.
    /// </summary>
    [HttpPost("sites/{siteSlug}/verify-token")]
    public async Task<ActionResult<VerifySiteTokenResponse>> VerifyToken(
        [FromRoute] string siteSlug,
        [FromBody] VerifySiteTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required");

        var clientIp = GetClientIp();
        var userAgent = Request.Headers.UserAgent.FirstOrDefault();

        var result = await siteTokenService.VerifyTokenAsync(
            siteSlug, request.Token, clientIp, userAgent, null, cancellationToken);

        if (!result.Success)
        {
            return Ok(new VerifySiteTokenResponse(false, null, null, null, result.ErrorMessage));
        }

        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == siteSlug, cancellationToken);
        var sessionToken = sessionService.CreateSession(result.SiteId!.Value, siteSlug);

        return Ok(new VerifySiteTokenResponse(true, sessionToken, siteSlug, site?.Name, null));
    }

    /// <summary>
    /// Check if the current session token is still valid.
    /// </summary>
    [HttpGet("session/validate")]
    public ActionResult ValidateSession()
    {
        if (!IsDynamicQrEnabled())
            return Ok(new { valid = true, dynamicQrEnabled = false });

        var sessionToken = Request.Headers["X-Session-Token"].FirstOrDefault();
        var session = sessionService.ValidateSession(sessionToken);

        return Ok(new
        {
            valid = session is not null,
            siteSlug = session?.SiteSlug,
            dynamicQrEnabled = true,
        });
    }

    [HttpPost("tickets/{ticketId:guid}/complete")]
    public async Task<ActionResult<RoomStatusDto>> CompleteByTicket(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CompleteByTicketIdAsync(ticketId, cancellationToken));
    }

    [HttpPost("tickets/{ticketId:guid}/feedback")]
    public async Task<ActionResult<FeedbackResponse>> SubmitFeedback(
        [FromRoute] Guid ticketId,
        [FromBody] FeedbackRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.SubmitFeedbackAsync(ticketId, request, cancellationToken));
    }

    [HttpPost("tickets/{ticketId:guid}/cancel")]
    public async Task<ActionResult<RoomStatusDto>> CancelTicket(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CancelTicketAsync(ticketId, cancellationToken));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool IsDynamicQrEnabled()
    {
        return configuration.GetValue("DynamicQr:Enabled", false);
    }

    private string? GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
