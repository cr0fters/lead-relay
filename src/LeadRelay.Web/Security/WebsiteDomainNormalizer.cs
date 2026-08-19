namespace LeadRelay.Web.Security;

internal static class WebsiteDomainNormalizer
{
    internal const int MaximumDomains = 20;

    internal static (IReadOnlyList<string> Domains, string? Error) NormalizeList(string? value)
    {
        var entries = (value ?? "")
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var domains = new List<string>();
        foreach (var entry in entries)
        {
            var domain = Normalize(entry);
            if (domain is null)
                return ([], $"Enter a valid website domain instead of '{entry}'. For example, example.com.");

            if (!domains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                domains.Add(domain);
        }

        if (domains.Count > MaximumDomains)
            return ([], $"Add no more than {MaximumDomains} website domains.");

        return (domains, null);
    }

    internal static string? Normalize(string? value)
    {
        var candidate = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 255)
            return null;

        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = $"https://{candidate}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.IdnHost) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo))
            return null;

        try
        {
            var host = uri.IdnHost.Trim().TrimEnd('.').ToLowerInvariant();
            return host.Length is > 0 and <= 255 && !host.Contains(' ') ? host : null;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
