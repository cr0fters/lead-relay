namespace LeadRelay.Contracts.Widget;

public sealed record WidgetTokenRequest(
    string SiteId,
    string PublicKey,
    string PageUrl,
    string Path,
    string? Referrer,
    Dictionary<string, string>? Utm,
    int? TzOffsetMinutes,
    string? Lang,
    string? Ua);
