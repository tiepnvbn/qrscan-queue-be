using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Data;
using QueueQr.Api.Dtos;
using QueueQr.Api.Services;

namespace QueueQr.Api.Controllers;

[ApiController]
[Route("api/public")]
public sealed class PublicController(QueueService queue, AppDbContext db) : ControllerBase
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
        var (ticket, status, myTicket) = await queue.TakeTicketAsync(siteSlug, roomSlug, request, cancellationToken);
        return Ok(new
        {
            ticketId = ticket.Id,
            number = ticket.Number,
            status,
            myTicket,
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
}
