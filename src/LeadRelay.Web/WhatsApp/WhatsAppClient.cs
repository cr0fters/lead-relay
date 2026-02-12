using System.Net.Http.Headers;
using System.Net.Http.Json;
using LeadRelay.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppClient(
    HttpClient http,
    IOptions<WhatsAppOptions> options,
    ISiteRepository sites,
    ILogger<WhatsAppClient> logger)
{
    public async Task<bool> SendTextAsync(string to, string body, string? siteId, CancellationToken ct)
    {
        var opts = options.Value;
        var senderPhoneNumberId = await ResolveSenderPhoneNumberIdAsync(siteId, ct);
        var senderConfig = ResolveSenderConfig(opts, senderPhoneNumberId);

        var endpoint = ResolveEndpoint(senderConfig.MessagesEndpoint, opts.MessagesEndpoint, senderPhoneNumberId);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.LogWarning("WhatsApp send skipped: no MessagesEndpoint resolved for site {SiteId}.", siteId ?? "<none>");
            return false;
        }

        var accessToken = ResolveAccessToken(senderConfig.AccessToken, opts.AccessToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning("WhatsApp send skipped: no AccessToken resolved for site {SiteId}.", siteId ?? "<none>");
            return false;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body }
        });

        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return true;

        var error = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("WhatsApp send failed: {StatusCode} {Body}", (int)response.StatusCode, error);
        return false;
    }

    private async Task<string?> ResolveSenderPhoneNumberIdAsync(string? siteId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(siteId))
            return null;

        var site = await sites.GetByIdAsync(siteId.Trim(), ct);
        return site?.WhatsAppPhoneNumberId?.Trim();
    }

    private static WhatsAppSenderOptions ResolveSenderConfig(WhatsAppOptions options, string? senderPhoneNumberId)
    {
        if (string.IsNullOrWhiteSpace(senderPhoneNumberId) || options.Senders is null)
            return new WhatsAppSenderOptions();

        return options.Senders.TryGetValue(senderPhoneNumberId, out var sender)
            ? sender ?? new WhatsAppSenderOptions()
            : new WhatsAppSenderOptions();
    }

    private static string? ResolveEndpoint(string? senderEndpoint, string? defaultEndpoint, string? senderPhoneNumberId)
    {
        var endpoint = string.IsNullOrWhiteSpace(senderEndpoint)
            ? defaultEndpoint
            : senderEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        var normalized = endpoint.Trim();
        if (!string.IsNullOrWhiteSpace(senderPhoneNumberId))
            normalized = normalized.Replace("{phone_number_id}", senderPhoneNumberId, StringComparison.OrdinalIgnoreCase)
                .Replace("<PHONE_NUMBER_ID>", senderPhoneNumberId, StringComparison.OrdinalIgnoreCase);

        return normalized;
    }

    private static string? ResolveAccessToken(string? senderAccessToken, string? defaultAccessToken)
    {
        if (!string.IsNullOrWhiteSpace(senderAccessToken))
            return senderAccessToken.Trim();

        return string.IsNullOrWhiteSpace(defaultAccessToken)
            ? null
            : defaultAccessToken.Trim();
    }
}
