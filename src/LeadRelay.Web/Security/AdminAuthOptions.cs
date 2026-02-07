namespace LeadRelay.Web.Security;

public sealed class AdminAuthOptions
{
    public string HeaderName { get; init; } = "X-Admin-Token";
    public string QueryParameterName { get; init; } = "adminToken";
    public string CookieName { get; init; } = "leadrelay_admin_token";
    public string? Token { get; init; }
}
