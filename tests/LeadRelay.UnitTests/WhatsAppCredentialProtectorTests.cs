using System.Security.Cryptography;
using LeadRelay.Web.WhatsApp;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppCredentialProtectorTests
{
    [Test]
    public void protect_round_trips_without_exposing_plaintext()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protector = new WhatsAppCredentialProtector(Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = key
        }));

        var protectedValue = protector.Protect("site_a", "secret-access-token");
        var unprotected = protector.TryUnprotect("site_a", protectedValue, out var value);

        Assert.That(protectedValue, Does.Not.Contain("secret-access-token"));
        Assert.That(unprotected, Is.True);
        Assert.That(value, Is.EqualTo("secret-access-token"));
        Assert.That(protector.TryUnprotect("site_b", protectedValue, out _), Is.False);
    }

    [Test]
    public void invalid_key_is_reported_as_not_configured()
    {
        var protector = new WhatsAppCredentialProtector(Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = "not-base64"
        }));

        Assert.That(protector.IsConfigured, Is.False);
    }
}
