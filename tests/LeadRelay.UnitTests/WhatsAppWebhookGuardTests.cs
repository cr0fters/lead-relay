using System.Security.Cryptography;
using System.Text;
using LeadRelay.Infrastructure.Time;
using LeadRelay.Web.WhatsApp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppWebhookGuardTests
{
    [Test]
    public void signature_validation_passes_when_disabled()
    {
        var guard = CreateGuard(new WhatsAppOptions
        {
            RequireSignatureValidation = false,
            AppSecret = ""
        });

        var isValid = guard.IsSignatureValid(Encoding.UTF8.GetBytes("{}"), null);

        Assert.That(isValid, Is.True);
    }

    [Test]
    public void signature_validation_requires_valid_header_when_enabled()
    {
        const string payload = "{\"hello\":\"world\"}";
        const string secret = "secret-123";
        var guard = CreateGuard(new WhatsAppOptions
        {
            RequireSignatureValidation = true,
            AppSecret = secret
        });

        var validSignature = BuildSignatureHeader(payload, secret);
        var valid = guard.IsSignatureValid(Encoding.UTF8.GetBytes(payload), validSignature);
        var invalid = guard.IsSignatureValid(Encoding.UTF8.GetBytes(payload), "sha256=deadbeef");

        Assert.That(valid, Is.True);
        Assert.That(invalid, Is.False);
    }

    [Test]
    public void idempotency_marks_message_as_duplicate_after_first_processing()
    {
        var guard = CreateGuard(new WhatsAppOptions
        {
            IdempotencyTtlMinutes = 10
        });

        var before = guard.IsDuplicate("site_a", "wamid.1");
        guard.MarkProcessed("site_a", "wamid.1");
        var after = guard.IsDuplicate("site_a", "wamid.1");

        Assert.That(before, Is.False);
        Assert.That(after, Is.True);
    }

    private static WhatsAppWebhookGuard CreateGuard(WhatsAppOptions options)
    {
        return new WhatsAppWebhookGuard(
            Options.Create(options),
            new MemoryCache(new MemoryCacheOptions()),
            new SystemClock());
    }

    private static string BuildSignatureHeader(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
