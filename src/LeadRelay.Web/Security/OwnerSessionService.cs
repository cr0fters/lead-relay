using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Tokens;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Security;

public sealed class OwnerSessionService(
    IOptions<OwnerPortalOptions> options,
    ISiteRepository sites,
    IOwnerSessionVersionStore sessionVersions,
    IWebHostEnvironment environment)
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

        Dictionary<string, string>? claims = null;
        foreach (var signingSecret in SigningSecrets())
        {
            var tokenService = new HmacTokenService(signingSecret);
            if (tokenService.TryValidate(token, out var validatedClaims))
            {
                claims = validatedClaims;
                break;
            }
        }

        if (claims is null) return null;

        if (!claims.TryGetValue("siteId", out var siteId) || string.IsNullOrWhiteSpace(siteId)) return null;
        if (!claims.TryGetValue("ownerEmail", out var ownerEmail) || string.IsNullOrWhiteSpace(ownerEmail)) return null;

        var normalizedSiteId = siteId.Trim();
        var normalizedOwnerEmail = ownerEmail.Trim();

        var site = await sites.GetByIdAsync(normalizedSiteId, ct);
        if (site is null) return null;
        if (!string.Equals(site.OwnerEmail, normalizedOwnerEmail, StringComparison.OrdinalIgnoreCase)) return null;

        var currentSessionVersion = await sessionVersions.GetAsync(site.Id, ct);
        if (currentSessionVersion is null) return null;
        var tokenSessionVersion = 1L;
        if (claims.TryGetValue("sessionVersion", out var versionClaim) &&
            (!long.TryParse(
                versionClaim,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out tokenSessionVersion) || tokenSessionVersion < 1))
        {
            return null;
        }

        if (tokenSessionVersion != currentSessionVersion.Value) return null;

        return new OwnerAuthContext(site.Id, site.OwnerEmail, currentSessionVersion.Value);
    }

    public void SignIn(HttpContext context, string token)
    {
        var maxAge = TimeSpan.FromHours(Math.Clamp(_options.SessionTtlHours, 1, 168));
        context.Response.Cookies.Append(
            _options.SessionCookieName,
            token,
            AuthCookieOptions.Create(context, environment, maxAge));
    }

    public void SignOut(HttpContext context)
    {
        context.Response.Cookies.Delete(
            _options.SessionCookieName,
            AuthCookieOptions.Create(context, environment));
    }

    public string CreateLoginToken(string siteId, string ownerEmail, long sessionVersion)
    {
        if (sessionVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(sessionVersion), "Session version must be positive.");

        var tokenService = new HmacTokenService(_options.SigningSecret);
        var ttl = TimeSpan.FromHours(Math.Clamp(_options.SessionTtlHours, 1, 168));
        return tokenService.CreateSignedToken(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["siteId"] = siteId,
                ["ownerEmail"] = ownerEmail,
                ["sessionVersion"] = sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            ttl);
    }

    private IEnumerable<string> SigningSecrets()
    {
        yield return _options.SigningSecret.Trim();

        var previousSecret = _options.PreviousSigningSecret?.Trim();
        if (!string.IsNullOrWhiteSpace(previousSecret) &&
            !string.Equals(previousSecret, _options.SigningSecret.Trim(), StringComparison.Ordinal))
        {
            yield return previousSecret;
        }
    }
}
