using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace LeadRelay.Web.WhatsApp;

public interface IWhatsAppWebhookGuard
{
    bool IsSignatureValid(ReadOnlySpan<byte> payloadBytes, string? signatureHeader);
    Task<bool> TryBeginProcessingAsync(string siteId, string messageId, CancellationToken ct);
    Task MarkSideEffectsStartedAsync(string siteId, string messageId, CancellationToken ct);
    Task MarkProcessedAsync(string siteId, string messageId, CancellationToken ct);
    Task AbandonAsync(string siteId, string messageId, CancellationToken ct);
}

public sealed class WhatsAppWebhookGuard(
    IOptions<WhatsAppOptions> options,
    LeadRelayDbContext db,
    IClock clock) : IWhatsAppWebhookGuard
{
    public bool IsSignatureValid(ReadOnlySpan<byte> payloadBytes, string? signatureHeader)
    {
        var settings = options.Value;
        if (!settings.RequireSignatureValidation)
            return true;

        var secret = (settings.AppSecret ?? "").Trim();
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var providedHex = signatureHeader[prefix.Length..].Trim();
        if (providedHex.Length != 64)
            return false;

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = hmac.ComputeHash(payloadBytes.ToArray());
        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }

    public async Task<bool> TryBeginProcessingAsync(string siteId, string messageId, CancellationToken ct)
    {
        var normalizedSiteId = Normalize(siteId);
        var normalizedMessageId = Normalize(messageId);
        if (string.IsNullOrWhiteSpace(normalizedSiteId) || string.IsNullOrWhiteSpace(normalizedMessageId))
            return false;

        var processingStaleBefore = clock.UtcNow.AddMinutes(-Math.Max(1, options.Value.IdempotencyProcessingLeaseMinutes));
        var processedExpiredBefore = clock.UtcNow.AddDays(-Math.Max(1, options.Value.ProcessedReceiptRetentionDays));
        var expired = await db.WhatsAppMessageReceipts
            .Where(x =>
                (x.ProcessedAtUtc.HasValue && x.ProcessedAtUtc.Value < processedExpiredBefore) ||
                (!x.ProcessedAtUtc.HasValue && x.SideEffectsStartedAtUtc.HasValue &&
                 x.SideEffectsStartedAtUtc.Value < processedExpiredBefore) ||
                (!x.ProcessedAtUtc.HasValue && !x.SideEffectsStartedAtUtc.HasValue &&
                 x.StartedAtUtc < processingStaleBefore))
            .OrderBy(x => x.StartedAtUtc)
            .Take(100)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            db.WhatsAppMessageReceipts.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
        }

        var existing = await db.WhatsAppMessageReceipts
            .FirstOrDefaultAsync(x => x.SiteId == normalizedSiteId && x.MessageId == normalizedMessageId, ct);
        if (existing is not null)
        {
            if (existing.ProcessedAtUtc.HasValue ||
                existing.SideEffectsStartedAtUtc.HasValue ||
                existing.StartedAtUtc >= processingStaleBefore)
                return false;

            db.WhatsAppMessageReceipts.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        var receipt = new WhatsAppMessageReceiptRecord
        {
            SiteId = normalizedSiteId,
            MessageId = normalizedMessageId,
            StartedAtUtc = clock.UtcNow
        };
        db.WhatsAppMessageReceipts.Add(receipt);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(receipt).State = EntityState.Detached;
            return false;
        }
    }

    public async Task MarkSideEffectsStartedAsync(string siteId, string messageId, CancellationToken ct)
    {
        var normalizedSiteId = Normalize(siteId);
        var normalizedMessageId = Normalize(messageId);
        var receipt = await db.WhatsAppMessageReceipts
            .FirstOrDefaultAsync(x => x.SiteId == normalizedSiteId && x.MessageId == normalizedMessageId, ct);
        if (receipt is null)
            throw new InvalidOperationException("WhatsApp receipt was not found before starting side effects.");
        receipt.SideEffectsStartedAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkProcessedAsync(string siteId, string messageId, CancellationToken ct)
    {
        var normalizedSiteId = Normalize(siteId);
        var normalizedMessageId = Normalize(messageId);
        var receipt = await db.WhatsAppMessageReceipts
            .FirstOrDefaultAsync(x => x.SiteId == normalizedSiteId && x.MessageId == normalizedMessageId, ct);
        if (receipt is null) return;
        receipt.ProcessedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AbandonAsync(string siteId, string messageId, CancellationToken ct)
    {
        var normalizedSiteId = Normalize(siteId);
        var normalizedMessageId = Normalize(messageId);
        var receipt = await db.WhatsAppMessageReceipts
            .FirstOrDefaultAsync(x => x.SiteId == normalizedSiteId && x.MessageId == normalizedMessageId, ct);
        if (receipt is null || receipt.ProcessedAtUtc.HasValue || receipt.SideEffectsStartedAtUtc.HasValue) return;
        db.WhatsAppMessageReceipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
    }

    private static string Normalize(string? value) => (value ?? "").Trim();
}
