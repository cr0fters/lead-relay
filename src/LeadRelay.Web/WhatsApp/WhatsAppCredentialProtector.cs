using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppCredentialProtector(IOptions<WhatsAppOptions> options)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly WhatsAppOptions _options = options.Value;

    public bool IsConfigured => TryGetKey(out _);

    public string Protect(string siteId, string value)
    {
        if (string.IsNullOrWhiteSpace(siteId))
            throw new ArgumentException("Site id is required.", nameof(siteId));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Credential value is required.", nameof(value));
        if (!TryGetKey(out var key))
            throw new InvalidOperationException("WhatsApp credential encryption is not configured.");

        var plaintext = System.Text.Encoding.UTF8.GetBytes(value.Trim());
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, GetAssociatedData(siteId));

        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        payload[0] = 1;
        Buffer.BlockCopy(nonce, 0, payload, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, 1 + NonceSize + TagSize, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public bool TryUnprotect(string siteId, string? protectedValue, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(protectedValue) || !TryGetKey(out var key))
            return false;

        try
        {
            var payload = Convert.FromBase64String(protectedValue);
            if (payload.Length <= 1 + NonceSize + TagSize || payload[0] != 1)
                return false;

            var nonce = payload.AsSpan(1, NonceSize);
            var tag = payload.AsSpan(1 + NonceSize, TagSize);
            var ciphertext = payload.AsSpan(1 + NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, GetAssociatedData(siteId));
            value = System.Text.Encoding.UTF8.GetString(plaintext);
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private bool TryGetKey(out byte[] key)
    {
        key = [];
        var encoded = _options.CredentialEncryptionKey?.Trim();
        if (string.IsNullOrWhiteSpace(encoded))
            return false;

        try
        {
            key = Convert.FromBase64String(encoded);
            return key.Length == 32;
        }
        catch (FormatException)
        {
            key = [];
            return false;
        }
    }

    private static byte[] GetAssociatedData(string siteId)
        => System.Text.Encoding.UTF8.GetBytes($"LeadRelay:WhatsApp:{siteId.Trim()}");
}
