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
}
