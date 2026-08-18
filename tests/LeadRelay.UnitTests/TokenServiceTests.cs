using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Tokens;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class TokenServiceTests
{
    [Test]
    public void round_trip()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var svc = new ShortCodeTokenService(clock);
        var token = svc.CreateSignedToken(new Dictionary<string, string> { ["siteId"] = "site_demo" }, TimeSpan.FromMinutes(1));
        Assert.That(svc.TryValidate(token, out var claims), Is.True);
        Assert.That(claims["siteId"], Is.EqualTo("site_demo"));
    }

    [Test]
    public void token_expires()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var svc = new ShortCodeTokenService(clock);
        var token = svc.CreateSignedToken(new Dictionary<string, string> { ["siteId"] = "site_demo" }, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.That(svc.TryValidate(token, out _), Is.False);
    }

    [TestCase("not-a-token")]
    [TestCase("%%%%.signature")]
    public void hmac_service_rejects_malformed_tokens_without_throwing(string token)
    {
        var service = new HmacTokenService("test-secret");

        Assert.That(service.TryValidate(token, out _), Is.False);
    }

    [Test]
    public void hmac_service_rejects_signed_malformed_payload_without_throwing()
    {
        const string secret = "test-secret";
        const string malformedPayload = "%%%";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(malformedPayload)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var service = new HmacTokenService(secret);

        Assert.That(service.TryValidate($"{malformedPayload}.{signature}", out _), Is.False);
    }


    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }
}
