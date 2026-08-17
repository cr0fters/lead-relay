using System.Security.Cryptography;
using System.Text;
using LeadRelay.Infrastructure.Time;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.WhatsApp;
using Microsoft.EntityFrameworkCore;
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
    public async Task idempotency_rejects_message_after_processing_started()
    {
        var guard = CreateGuard(new WhatsAppOptions
        {
            IdempotencyProcessingLeaseMinutes = 10,
            ProcessedReceiptRetentionDays = 30
        });

        var before = await guard.TryBeginProcessingAsync("site_a", "wamid.1", CancellationToken.None);
        await guard.MarkProcessedAsync("site_a", "wamid.1", CancellationToken.None);
        var after = await guard.TryBeginProcessingAsync("site_a", "wamid.1", CancellationToken.None);

        Assert.That(before, Is.True);
        Assert.That(after, Is.False);
    }

    [Test]
    public async Task processed_receipt_survives_processing_lease_expiry()
    {
        using var db = CreateDb();
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        db.WhatsAppMessageReceipts.Add(new WhatsAppMessageReceiptRecord
        {
            SiteId = "site_a",
            MessageId = "wamid.processed",
            StartedAtUtc = now.AddHours(-2),
            ProcessedAtUtc = now.AddHours(-2)
        });
        await db.SaveChangesAsync();
        var guard = new WhatsAppWebhookGuard(
            Options.Create(new WhatsAppOptions
            {
                IdempotencyProcessingLeaseMinutes = 10,
                ProcessedReceiptRetentionDays = 30
            }),
            db,
            new FixedClock(now));

        var accepted = await guard.TryBeginProcessingAsync("site_a", "wamid.processed", CancellationToken.None);

        Assert.That(accepted, Is.False);
        Assert.That(await db.WhatsAppMessageReceipts.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task receipt_is_retained_once_external_side_effects_start()
    {
        using var db = CreateDb();
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        var guard = new WhatsAppWebhookGuard(
            Options.Create(new WhatsAppOptions
            {
                IdempotencyProcessingLeaseMinutes = 10,
                ProcessedReceiptRetentionDays = 30
            }),
            db,
            new FixedClock(now));
        Assert.That(await guard.TryBeginProcessingAsync("site_a", "wamid.side-effect", CancellationToken.None), Is.True);

        await guard.MarkSideEffectsStartedAsync("site_a", "wamid.side-effect", CancellationToken.None);
        await guard.AbandonAsync("site_a", "wamid.side-effect", CancellationToken.None);
        var retryAccepted = await guard.TryBeginProcessingAsync("site_a", "wamid.side-effect", CancellationToken.None);

        Assert.That(retryAccepted, Is.False);
        var receipt = await db.WhatsAppMessageReceipts.SingleAsync();
        Assert.That(receipt.SideEffectsStartedAtUtc, Is.EqualTo(now));
    }

    private static WhatsAppWebhookGuard CreateGuard(WhatsAppOptions options)
    {
        return new WhatsAppWebhookGuard(
            Options.Create(options),
            CreateDb(),
            new SystemClock());
    }

    private static LeadRelayDbContext CreateDb()
    {
        var dbOptions = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"whatsapp-guard-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(dbOptions);
    }

    private static string BuildSignatureHeader(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private sealed class FixedClock(DateTimeOffset now) : LeadRelay.Application.Abstractions.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
