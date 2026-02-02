using LeadRelay.Infrastructure.Tokens;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class TokenServiceTests
{
    [Test]
    public void round_trip()
    {
        var svc = new HmacTokenService("secret_1234567890");
        var token = svc.CreateSignedToken(new Dictionary<string, string> { ["siteId"] = "site_demo" }, TimeSpan.FromMinutes(1));
        Assert.That(svc.TryValidate(token, out var claims), Is.True);
        Assert.That(claims["siteId"], Is.EqualTo("site_demo"));
    }
}
