using Microsoft.AspNetCore.Mvc;
using QueueQr.Api.Services;

namespace QueueQr.Api.Controllers;

[ApiController]
[Route("api/staff/sites/{siteSlug}/rooms/{roomSlug}")]
public sealed class StaffController(QueueService queue) : ControllerBase
{
    [HttpGet("waiting")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetWaiting(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.GetWaitingListAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("call-next")]
    public async Task<ActionResult> CallNext(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CallNextAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("complete-current")]
    public async Task<ActionResult> CompleteCurrent(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.CompleteCurrentAsync(siteSlug, roomSlug, cancellationToken));
    }

    [HttpPost("skip-current")]
    public async Task<ActionResult> SkipCurrent(
        [FromRoute] string siteSlug,
        [FromRoute] string roomSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.SkipCurrentAsync(siteSlug, roomSlug, cancellationToken));
    }
}
