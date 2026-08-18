using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LeadRelay.Web.Extensions;

public static class ConfigurationValidationExtensions
{
    public static void ValidateRequiredSecrets(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment()) return;

        var missing = new List<string>();

        Require(builder.Configuration, "ConnectionStrings:LeadRelay", missing);
        Require(builder.Configuration, "AdminAuth:Token", missing);
        Require(builder.Configuration, "OwnerPortal:SigningSecret", missing);
        Require(builder.Configuration, "PublicBaseUrl", missing);
        if (!IsValidPublicBaseUrl(builder.Configuration["PublicBaseUrl"]) &&
            !missing.Contains("PublicBaseUrl", StringComparer.Ordinal))
        {
            missing.Add("PublicBaseUrl (must be an absolute HTTPS URL)");
        }
        Require(builder.Configuration, "Postmark:ServerToken", missing);
        Require(builder.Configuration, "Postmark:FromEmail", missing);
        Require(builder.Configuration, "WhatsApp:VerifyToken", missing);
        Require(builder.Configuration, "WhatsApp:AppSecret", missing);
        Require(builder.Configuration, "WhatsApp:CredentialEncryptionKey", missing);
        if (!IsValidEncryptionKey(builder.Configuration["WhatsApp:CredentialEncryptionKey"]) &&
            !missing.Contains("WhatsApp:CredentialEncryptionKey", StringComparer.Ordinal))
        {
            missing.Add("WhatsApp:CredentialEncryptionKey (must be base64 for exactly 32 bytes)");
        }

        if (!builder.Configuration.GetValue<bool>("WhatsApp:RequireSignatureValidation"))
            missing.Add("WhatsApp:RequireSignatureValidation=true");

        if (missing.Count == 0) return;

        var joined = string.Join(", ", missing);
        throw new InvalidOperationException(
            $"Missing required production configuration values: {joined}. Provide these via environment variables or a secure configuration provider.");
    }

    private static void Require(IConfiguration configuration, string key, List<string> missing)
    {
        var value = configuration[key];
        if (IsUnset(value)) missing.Add(key);
    }

    private static bool IsUnset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        var trimmed = value.Trim();
        if (trimmed.Contains("change_me", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Contains("paste_your", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static bool IsValidEncryptionKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            return Convert.FromBase64String(value.Trim()).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidPublicBaseUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
           string.IsNullOrEmpty(uri.UserInfo) &&
           string.IsNullOrEmpty(uri.Query) &&
           string.IsNullOrEmpty(uri.Fragment);
}
