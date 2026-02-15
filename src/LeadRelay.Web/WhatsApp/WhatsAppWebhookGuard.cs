using LeadRelay.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace LeadRelay.Web.WhatsApp;

public interface IWhatsAppWebhookGuard
{
    bool IsSignatureValid(ReadOnlySpan<byte> payloadBytes, string? signatureHeader);
    bool IsDuplicate(string siteId, string messageId);
    void MarkProcessed(string siteId, string messageId);
}

public sealed class WhatsAppWebhookGuard(
    IOptions<WhatsAppOptions> options,
    IMemoryCache cache,
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

    public bool IsDuplicate(string siteId, string messageId)
    {
        var key = BuildIdempotencyKey(siteId, messageId);
        return cache.TryGetValue(key, out _);
    }

    public void MarkProcessed(string siteId, string messageId)
    {
        var ttlMinutes = Math.Max(1, options.Value.IdempotencyTtlMinutes);
        var key = BuildIdempotencyKey(siteId, messageId);
        var expiresAt = clock.UtcNow.AddMinutes(ttlMinutes);
        cache.Set(key, true, expiresAt);
    }

    private static string BuildIdempotencyKey(string siteId, string messageId)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedMessageId = (messageId ?? "").Trim();
        return $"whatsapp:{normalizedSiteId}:{normalizedMessageId}";
    }
}
