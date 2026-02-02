using System.Collections.Concurrent;
using System.Security.Cryptography;
using LeadRelay.Application.Abstractions;

namespace LeadRelay.Infrastructure.Tokens;

public sealed class ShortCodeTokenService(IClock clock) : ITokenService
{
    private readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.Ordinal);

    public string CreateSignedToken(Dictionary<string, string> claims, TimeSpan ttl)
    {
        var exp = clock.UtcNow.Add(ttl);
        var token = CreateUniqueToken();
        _store[token] = new Entry(new Dictionary<string, string>(claims), exp);
        return token;
    }

    public bool TryValidate(string token, out Dictionary<string, string> claims)
    {
        claims = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(token)) return false;

        if (!_store.TryGetValue(token, out var entry)) return false;
        if (clock.UtcNow > entry.ExpiresAtUtc)
        {
            _store.TryRemove(token, out _);
            return false;
        }

        claims = entry.Claims;
        return true;
    }

    private string CreateUniqueToken()
    {
        for (var i = 0; i < 5; i++)
        {
            var token = GenerateToken();
            if (!_store.ContainsKey(token)) return token;
        }

        // If collisions persist, keep trying until we find a free token.
        while (true)
        {
            var token = GenerateToken();
            if (!_store.ContainsKey(token)) return token;
        }
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url(bytes);
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record Entry(Dictionary<string, string> Claims, DateTimeOffset ExpiresAtUtc);
}
