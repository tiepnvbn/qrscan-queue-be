using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Data;
using QueueQr.Api.Dtos;
using QueueQr.Api.Entities;
using QueueQr.Api.Services;

namespace QueueQr.Api.Controllers;

[ApiController]
[Route("api/staff")]
public sealed class StaffController(QueueService queue, AppDbContext db, IpWhitelistService ipWhitelist) : ControllerBase
{
    // ── Phase 2: Authentication + Ticket management ──────────────────

    [HttpPost("login")]
    public async Task<ActionResult<StaffLoginResponse>> Login(
        [FromBody] StaffLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Phone and password are required");

        var result = await queue.StaffLoginAsync(request, cancellationToken);
        if (result is null)
            return Unauthorized("Invalid phone or password");

        // Validate that staff's site matches the IP-derived site
        if (ipWhitelist.IsEnabled())
        {
            var clientIp = GetClientIp();
            if (!ipWhitelist.IsStaffAllowedFromIp(result.SiteSlug, clientIp))
            {
                return StatusCode(403, new
                {
                    error = "Bạn chỉ có thể đăng nhập tại cơ sở của mình.",
                    code = "STAFF_SITE_MISMATCH"
                });
            }
        }

        return Ok(result);
    }

    [HttpGet("sites/{siteSlug}/tickets")]
    public async Task<ActionResult<StaffTicketListResponse>> ListTickets(
        [FromRoute] string siteSlug,
        [FromQuery] string? roomSlug,
        [FromQuery] string? search,
        [FromQuery] string[]? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var staff = await GetAuthStaffAsync(cancellationToken);
        if (staff is null) return Unauthorized();

        return Ok(await queue.ListTicketsForStaffAsync(siteSlug, roomSlug, search, status, page, pageSize, cancellationToken));
    }

    [HttpPost("tickets/{ticketId:guid}/complete")]
    public async Task<ActionResult<RoomStatusDto>> CompleteTicket(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken)
    {
        var staff = await GetAuthStaffAsync(cancellationToken);
        if (staff is null) return Unauthorized();

        return Ok(await queue.StaffCompleteTicketAsync(ticketId, cancellationToken));
    }

    [HttpPost("tickets/{ticketId:guid}/cancel")]
    public async Task<ActionResult<RoomStatusDto>> CancelTicket(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken)
    {
        var staff = await GetAuthStaffAsync(cancellationToken);
        if (staff is null) return Unauthorized();

        return Ok(await queue.StaffCancelTicketAsync(ticketId, cancellationToken));
    }

    // ── Phase 1: Room-level actions (backward compatible) ────────────

    [HttpGet("sites/{siteSlug}/rooms/{roomSlug}/waiting")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetWaiting(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.GetWaitingListAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("sites/{siteSlug}/rooms/{roomSlug}/call-next")]
    public async Task<ActionResult> CallNext(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CallNextAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("sites/{siteSlug}/rooms/{roomSlug}/complete-current")]
    public async Task<ActionResult> CompleteCurrent(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CompleteCurrentAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("sites/{siteSlug}/rooms/{roomSlug}/skip-current")]
    public async Task<ActionResult> SkipCurrent(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.SkipCurrentAsync(siteSlug, roomSlug, cancellationToken));
    }

    // ── Auth helper ──────────────────────────────────────────────────

    private async Task<Staff?> GetAuthStaffAsync(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ")) return null;
        var token = auth["Bearer ".Length..];
        if (!Guid.TryParse(token, out var staffId)) return null;
        return await db.StaffMembers.Include(s => s.Site).FirstOrDefaultAsync(s => s.Id == staffId, ct);
    }

    private string? GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
