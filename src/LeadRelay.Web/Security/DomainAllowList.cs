using System.Globalization;

namespace LeadRelay.Web.Security;

internal static class DomainAllowList
{
    internal static bool IsAllowedDomain(IReadOnlyList<string> allowedDomains, string? referer, string? origin, string? fallbackHost = null)
    {
        if (allowedDomains.Count == 0) return true;

        var host = TryGetHost(referer) ?? TryGetHost(origin) ?? NormalizeDomain(fallbackHost);
        if (string.IsNullOrWhiteSpace(host)) return false;

        foreach (var allowed in allowedDomains)
        {
            var normalized = NormalizeDomain(allowed);
            if (normalized.Length == 0) continue;

            if (string.Equals(host, normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (host.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return "";
        return domain.Trim().Trim('.').ToLower(CultureInfo.InvariantCulture);
    }

    private static string? TryGetHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        return uri.Host;
    }
}
