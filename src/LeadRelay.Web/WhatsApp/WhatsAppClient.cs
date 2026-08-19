using System.Net.Http.Headers;
using System.Net.Http.Json;
using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppClient(
    HttpClient http,
    IOptions<WhatsAppOptions> options,
    ISiteRepository sites,
    LeadRelayDbContext db,
    WhatsAppCredentialProtector protector,
    ILogger<WhatsAppClient> logger)
{
    public async Task<bool> SendTextAsync(string to, string body, string? siteId, CancellationToken ct)
    {
        var opts = options.Value;
        var senderPhoneNumberId = await ResolveSenderPhoneNumberIdAsync(siteId, ct);
        var storedConnection = string.IsNullOrWhiteSpace(siteId)
            ? null
            : await db.WhatsAppConnections.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == siteId.Trim(), ct);
        var senderConfig = ResolveSenderConfig(opts, senderPhoneNumberId);

        var endpoint = ResolveEndpoint(senderConfig.MessagesEndpoint, opts.MessagesEndpoint, senderPhoneNumberId)
            ?? WhatsAppGraphApiEndpoints.BuildMessages(opts, senderPhoneNumberId);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.LogWarning("WhatsApp send skipped: no MessagesEndpoint resolved for site {SiteId}.", siteId ?? "<none>");
            await RecordConnectionFailureAsync(siteId, "WhatsApp message endpoint is not configured.", ct);
            return false;
        }

        var storedAccessToken = storedConnection is not null &&
                                string.Equals(storedConnection.PhoneNumberId, senderPhoneNumberId, StringComparison.Ordinal) &&
                                protector.TryUnprotect(storedConnection.SiteId, storedConnection.AccessTokenCiphertext, out var decrypted)
            ? decrypted
            : null;
        var accessToken = ResolveAccessToken(storedAccessToken, senderConfig.AccessToken, opts.AccessToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning("WhatsApp send skipped: no AccessToken resolved for site {SiteId}.", siteId ?? "<none>");
            await RecordConnectionFailureAsync(siteId, "The stored WhatsApp credential is unavailable.", ct);
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body }
        });

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "WhatsApp send request failed for site {SiteId}.", siteId ?? "<none>");
            await RecordConnectionFailureAsync(siteId, "WhatsApp could not be reached while sending a message.", ct);
            return false;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            await RecordConnectionFailureAsync(siteId, "WhatsApp did not respond in time.", ct);
            return false;
        }

        using (response)
        {
        if (response.IsSuccessStatusCode) return true;

        logger.LogWarning("WhatsApp send failed with status {StatusCode} for site {SiteId}.", (int)response.StatusCode, siteId ?? "<none>");
        await RecordConnectionFailureAsync(siteId, "WhatsApp rejected an outbound message. Reconnect or verify the sender configuration.", ct);
        return false;
        }
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

    private static string? ResolveAccessToken(string? storedAccessToken, string? senderAccessToken, string? defaultAccessToken)
    {
        if (!string.IsNullOrWhiteSpace(storedAccessToken))
            return storedAccessToken.Trim();

        if (!string.IsNullOrWhiteSpace(senderAccessToken))
            return senderAccessToken.Trim();

        return string.IsNullOrWhiteSpace(defaultAccessToken)
            ? null
            : defaultAccessToken.Trim();
    }

    private async Task RecordConnectionFailureAsync(string? siteId, string error, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(siteId)) return;
        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == siteId.Trim(), ct);
        if (connection is null) return;
        connection.Status = WhatsAppConnectionStatuses.ActionRequired;
        connection.LastError = error;
        connection.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
