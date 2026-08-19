namespace LeadRelay.Web.WhatsApp;

internal static class WhatsAppGraphApiEndpoints
{
    internal static string Build(WhatsAppOptions options, string relativePath)
    {
        var baseUrl = options.GraphApiBaseUrl.TrimEnd('/');
        var version = options.GraphApiVersion.Trim('/');
        return $"{baseUrl}/{version}/{relativePath.TrimStart('/')}";
    }

    internal static string? BuildMessages(WhatsAppOptions options, string? phoneNumberId)
        => string.IsNullOrWhiteSpace(phoneNumberId)
            ? null
            : Build(options, $"{phoneNumberId.Trim()}/messages");
}
