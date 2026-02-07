using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Tokens;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Security;

public sealed class OwnerSessionService(
    IOptions<OwnerPortalOptions> options,
    ISiteRepository sites)
{
    private readonly OwnerPortalOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SigningSecret);

    public string? GetSessionToken(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(_options.SessionCookieName, out var token) && !string.IsNullOrWhiteSpace(token))
            return token.Trim();

        return null;
    }

    public async Task<OwnerAuthContext?> ValidateAsync(string? token, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(token)) return null;

        var tokenService = new HmacTokenService(_options.SigningSecret);
        if (!tokenService.TryValidate(token, out var claims)) return null;

        if (!claims.TryGetValue("siteId", out var siteId) || string.IsNullOrWhiteSpace(siteId)) return null;
        if (!claims.TryGetValue("ownerEmail", out var ownerEmail) || string.IsNullOrWhiteSpace(ownerEmail)) return null;

        var normalizedSiteId = siteId.Trim();
        var normalizedOwnerEmail = ownerEmail.Trim();

        var site = await sites.GetByIdAsync(normalizedSiteId, ct);
        if (site is null) return null;
        if (!string.Equals(site.OwnerEmail, normalizedOwnerEmail, StringComparison.OrdinalIgnoreCase)) return null;

        return new OwnerAuthContext(site.Id, site.OwnerEmail);
    }

    public void SignIn(HttpContext context, string token)
    {
        var maxAge = TimeSpan.FromHours(Math.Clamp(_options.SessionTtlHours, 1, 168));
        context.Response.Cookies.Append(_options.SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = maxAge
        });
    }

    public void SignOut(HttpContext context)
    {
        context.Response.Cookies.Delete(_options.SessionCookieName);
    }

    public string CreateLoginToken(string siteId, string ownerEmail, TimeSpan ttl)
    {
        var tokenService = new HmacTokenService(_options.SigningSecret);
        return tokenService.CreateSignedToken(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["siteId"] = siteId,
                ["ownerEmail"] = ownerEmail
            },
            ttl);
    }
}
