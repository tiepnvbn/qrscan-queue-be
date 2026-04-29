using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Data;
using QueueQr.Api.Entities;

namespace QueueQr.Api.Services;

public sealed class SiteTokenService(AppDbContext db, IClock clock)
{
    private const int TokenLifetimeSeconds = 300;
    private const int TokenLength = 32;

    /// <summary>
    /// Gets the current active token for a site, creating one if none exists or the current one is expired/used.
    /// </summary>
    public async Task<SiteQrToken> GetOrCreateTokenAsync(Guid siteId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        // Fetch recent tokens for this site and filter in-memory
        // (SQLite provider cannot translate DateTimeOffset comparisons/ordering)
        var candidates = await db.SiteQrTokens
            .Where(t => t.SiteId == siteId)
            .ToListAsync(ct);

        var active = candidates
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault(t => t.ExpiresAt > now && t.UsedAt == null);

        if (active is not null)
            return active;

        return await CreateTokenAsync(siteId, ct);
    }

    /// <summary>
    /// Verifies a QR token and marks it as used. Returns the site ID if valid, null otherwise.
    /// </summary>
    public async Task<VerifyTokenResult> VerifyTokenAsync(
        string siteSlug,
        string token,
        string? ipAddress,
        string? userAgent,
        Guid? customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null)
            return new VerifyTokenResult(false, null, "Site not found");

        var qrToken = await db.SiteQrTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.SiteId == site.Id, ct);

        var success = false;
        string? errorMessage = null;

        if (qrToken is null)
        {
            errorMessage = "Mã QR không hợp lệ";
        }
        else if (qrToken.UsedAt is not null)
        {
            errorMessage = "Mã QR đã được sử dụng, vui lòng scan lại";
        }
        else if (qrToken.ExpiresAt <= now)
        {
            errorMessage = "Mã QR đã hết hạn, vui lòng scan lại";
        }
        else
        {
            // Mark token as used
            qrToken.UsedAt = now;
            qrToken.UsedByCustomerId = customerId;
            success = true;
        }

        // Log the scan attempt
        db.QrScanLogs.Add(new QrScanLog
        {
            SiteId = site.Id,
            Token = token,
            CustomerId = customerId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ScannedAt = now,
            Success = success,
        });

        await db.SaveChangesAsync(ct);

        // Immediately create a new token to replace the used/expired one
        if (success)
        {
            await CreateTokenAsync(site.Id, ct);
        }

        return new VerifyTokenResult(success, success ? site.Id : null, errorMessage);
    }

    /// <summary>
    /// Generates a new session token (simple GUID-based) after successful QR verification.
    /// In a production system this would be a JWT with claims.
    /// </summary>
    public static string GenerateSessionToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private async Task<SiteQrToken> CreateTokenAsync(Guid siteId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var token = new SiteQrToken
        {
            SiteId = siteId,
            Token = GenerateUrlSafeToken(),
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(TokenLifetimeSeconds),
        };

        db.SiteQrTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return token;
    }

    private static string GenerateUrlSafeToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Cleanup expired tokens older than 1 hour to keep the table small.
    /// Called periodically or on token creation.
    /// </summary>
    public async Task CleanupExpiredTokensAsync(CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddHours(-1);
        await db.SiteQrTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}

public sealed record VerifyTokenResult(bool Success, Guid? SiteId, string? ErrorMessage);
