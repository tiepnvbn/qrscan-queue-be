using QueueQr.Api.Services;

namespace QueueQr.Api.Middleware;

public sealed class StaffIpWhitelistMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to staff API endpoints
        if (context.Request.Path.StartsWithSegments("/api/staff", StringComparison.OrdinalIgnoreCase))
        {
            var ipService = context.RequestServices.GetRequiredService<IpWhitelistService>();

            if (ipService.IsEnabled())
            {
                var clientIp = GetClientIp(context);
                var allowedSiteSlug = ipService.GetAllowedSiteSlug(clientIp);

                if (allowedSiteSlug is null)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Không thể truy cập từ mạng này. Vui lòng sử dụng mạng tại cơ sở.",
                        code = "IP_NOT_WHITELISTED"
                    });
                    return;
                }

                // Store the site slug derived from IP for downstream use
                context.Items["IpSiteSlug"] = allowedSiteSlug;
            }
        }

        await next(context);
    }

    private static string? GetClientIp(HttpContext context)
    {
        // Check X-Forwarded-For header (common behind reverse proxies like Render, Nginx)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // Take the first IP (original client) from comma-separated list
            return forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
