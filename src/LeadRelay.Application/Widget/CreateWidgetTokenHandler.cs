using LeadRelay.Application.Abstractions;
using LeadRelay.Contracts.Widget;

namespace LeadRelay.Application.Widget;

public sealed class CreateWidgetTokenHandler(
    ISiteRepository sites,
    ITokenService tokens,
    IClock clock)
{
    public async Task<WidgetTokenResponse?> HandleAsync(WidgetTokenRequest request, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(request.SiteId, ct);
        if (site is null) return null;
        if (!FixedTimeEquals(site.PublicKey, request.PublicKey)) return null;

        var claims = new Dictionary<string, string>
        {
            ["siteId"] = site.Id,
            ["pageUrl"] = request.PageUrl ?? "",
            ["path"] = request.Path ?? "",
            ["iat"] = clock.UtcNow.ToUnixTimeSeconds().ToString()
        };

        if (!string.IsNullOrWhiteSpace(request.Referrer)) claims["ref"] = request.Referrer!;

        if (request.Utm is { Count: > 0 })
        {
            foreach (var kv in request.Utm)
                claims[$"utm:{kv.Key}"] = kv.Value;
        }

        var token = tokens.CreateSignedToken(claims, TimeSpan.FromMinutes(15));
        var prefillText = $"Hi ref={token}";
        return new WidgetTokenResponse(token, prefillText);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a ?? "");
        var bb = System.Text.Encoding.UTF8.GetBytes(b ?? "");
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
