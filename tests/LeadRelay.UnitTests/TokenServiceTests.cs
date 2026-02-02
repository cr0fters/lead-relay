using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Tokens;
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


    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }
}
