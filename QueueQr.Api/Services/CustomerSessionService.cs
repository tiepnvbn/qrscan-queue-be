using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace QueueQr.Api.Services;

/// <summary>
/// In-memory session store for customer sessions after QR verification.
/// Sessions are scoped to a site and expire after 2 hours.
/// </summary>
public sealed class CustomerSessionService
{
    private readonly ConcurrentDictionary<string, CustomerSession> _sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(2);

    public string CreateSession(Guid siteId, string siteSlug)
    {
        CleanupExpired();
        var token = GenerateToken();
        _sessions[token] = new CustomerSession(siteId, siteSlug, DateTimeOffset.UtcNow.Add(SessionLifetime));
        return token;
    }

    public CustomerSession? ValidateSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (!_sessions.TryGetValue(token, out var session))
            return null;

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return session;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.ExpiresAt <= now)
                _sessions.TryRemove(kvp.Key, out _);
        }
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

public sealed record CustomerSession(Guid SiteId, string SiteSlug, DateTimeOffset ExpiresAt);
