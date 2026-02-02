using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppClient(HttpClient http, IOptions<WhatsAppOptions> options, ILogger<WhatsAppClient> logger)
{
    public async Task<bool> SendTextAsync(string to, string body, CancellationToken ct)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.MessagesEndpoint))
        {
            logger.LogWarning("WhatsApp send skipped: WhatsApp:MessagesEndpoint is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(opts.AccessToken))
        {
            logger.LogWarning("WhatsApp send skipped: WhatsApp:AccessToken is not configured.");
            return false;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, opts.MessagesEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.AccessToken);
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
}
