using System.Net;

namespace QueueQr.Api.Services;

public sealed class IpWhitelistService(IConfiguration configuration)
{
    /// <summary>
    /// Check if the given IP address is allowed for any site.
    /// Returns the site slug if allowed, null if not.
    /// </summary>
    public string? GetAllowedSiteSlug(string? ipAddress)
    {
        if (!IsEnabled())
            return "any"; // Whitelist disabled — allow all

        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        var sitesSection = configuration.GetSection("IpWhitelist:Sites");
        foreach (var siteSection in sitesSection.GetChildren())
        {
            var siteSlug = siteSection.Key;
            var allowedIps = siteSection.Get<string[]>() ?? [];

            if (IsIpInList(ipAddress, allowedIps))
                return siteSlug;
        }

        return null;
    }

    /// <summary>
    /// Check if IP whitelist enforcement is enabled.
    /// </summary>
    public bool IsEnabled()
    {
        return configuration.GetValue("IpWhitelist:Enabled", false);
    }

    /// <summary>
    /// Validate that a staff member's site matches the site associated with their IP address.
    /// </summary>
    public bool IsStaffAllowedFromIp(string staffSiteSlug, string? ipAddress)
    {
        if (!IsEnabled())
            return true;

        var allowedSiteSlug = GetAllowedSiteSlug(ipAddress);
        if (allowedSiteSlug is null)
            return false;

        // "any" means whitelist is disabled
        if (allowedSiteSlug == "any")
            return true;

        return string.Equals(allowedSiteSlug, staffSiteSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIpInList(string ipAddress, string[] allowedIps)
    {
        if (!IPAddress.TryParse(ipAddress, out var clientIp))
            return false;

        foreach (var allowed in allowedIps)
        {
            // Support CIDR notation (e.g., "192.168.1.0/24")
            if (allowed.Contains('/'))
            {
                if (IsIpInCidr(clientIp, allowed))
                    return true;
            }
            else
            {
                // Exact IP match
                if (IPAddress.TryParse(allowed, out var allowedIp) &&
                    clientIp.Equals(allowedIp))
                    return true;
            }
        }

        return false;
    }

    private static bool IsIpInCidr(IPAddress ip, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefixLength))
            return false;

        if (ip.AddressFamily != network.AddressFamily)
            return false;

        var networkBytes = network.GetAddressBytes();
        var ipBytes = ip.GetAddressBytes();

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != networkBytes[i])
                return false;
        }

        if (remainingBits > 0 && fullBytes < networkBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((ipBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }
}
