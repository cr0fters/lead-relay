using LeadRelay.Application.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace LeadRelay.Infrastructure.Tokens;

public sealed class HmacTokenService(string secret) : ITokenService
{
    public string CreateSignedToken(Dictionary<string, string> claims, TimeSpan ttl)
    {
        var exp = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var copy = new Dictionary<string, string>(claims)
        {
            ["exp"] = exp.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var payload = Encode(copy);
        var sig = Sign(payload);
        return $"{payload}.{sig}";
    }

    public bool TryValidate(string token, out Dictionary<string, string> claims)
    {
        claims = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.', 2);
        if (parts.Length != 2) return false;

        var payload = parts[0];
        var sig = parts[1];

        var expected = Sign(payload);
        if (!FixedTimeEquals(sig, expected)) return false;

        Dictionary<string, string> decoded;
        try
        {
            decoded = Decode(payload);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!decoded.TryGetValue("exp", out var expRaw)) return false;
        if (!long.TryParse(
                expRaw,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var exp)) return false;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;

        claims = decoded;
        return true;
    }

    string Sign(string payload)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Base64Url(bytes);
    }

    static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    static string Encode(Dictionary<string, string> claims)
    {
        var pairs = claims
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Escape(kv.Key)}={Escape(kv.Value)}");
        return Base64Url(Encoding.UTF8.GetBytes(string.Join("&", pairs)));
    }

    static Dictionary<string, string> Decode(string payload)
    {
        var raw = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = Unescape(part[..idx]);
            var v = Unescape(part[(idx + 1)..]);
            dict[k] = v;
        }
        return dict;
    }

    static string Escape(string s) => Uri.EscapeDataString(s ?? "");
    static string Unescape(string s) => Uri.UnescapeDataString(s ?? "");

    static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
