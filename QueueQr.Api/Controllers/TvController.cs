using Microsoft.AspNetCore.Mvc;
using QueueQr.Api.Dtos;
using QueueQr.Api.Services;

namespace QueueQr.Api.Controllers;

[ApiController]
[Route("api/tv/sites/{siteSlug}")]
public sealed class TvController(QueueService queue) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SiteStatusDto>> GetStatus(
        [FromRoute] string siteSlug,
        CancellationToken cancellationToken)
    {
        return Ok(await queue.GetSiteStatusAsync(siteSlug, cancellationToken));
    }
}
