using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LeadRelay.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeadRelay.Infrastructure.Email;

public sealed class PostmarkEmailSender(
    HttpClient httpClient,
    IOptions<PostmarkOptions> options,
    ILogger<PostmarkEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostmarkOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Postmark email sender is disabled. Skipping outbound email to {ToEmail}.", toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ServerToken) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Postmark is enabled but ServerToken/FromEmail are missing.");
        }

        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromEmail.Trim()
            : $"{_options.FromName.Trim()} <{_options.FromEmail.Trim()}>";

        var request = new HttpRequestMessage(HttpMethod.Post, "email")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                From = from,
                To = toEmail,
                Subject = subject,
                TextBody = bodyText
            }, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Postmark-Server-Token", _options.ServerToken.Trim());

        await SendAndValidateAsync(request, ct);
    }

    public async Task SendTemplateAsync(
        string toEmail,
        string? templateAlias,
        int? templateId,
        IReadOnlyDictionary<string, string> templateModel,
        CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Postmark email sender is disabled. Skipping template email to {ToEmail}.", toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ServerToken) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Postmark is enabled but ServerToken/FromEmail are missing.");
        }

        if (string.IsNullOrWhiteSpace(templateAlias) && !templateId.HasValue)
        {
            throw new InvalidOperationException("Postmark template send requires template alias or template id.");
        }

        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromEmail.Trim()
            : $"{_options.FromName.Trim()} <{_options.FromEmail.Trim()}>";

        var payload = new Dictionary<string, object?>
        {
            ["From"] = from,
            ["To"] = toEmail,
            ["TemplateModel"] = templateModel
        };

        if (!string.IsNullOrWhiteSpace(templateAlias))
            payload["TemplateAlias"] = templateAlias.Trim();
        if (templateId.HasValue)
            payload["TemplateId"] = templateId.Value;

        var request = new HttpRequestMessage(HttpMethod.Post, "email/withTemplate")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Postmark-Server-Token", _options.ServerToken.Trim());

        await SendAndValidateAsync(request, ct);
    }

    private async Task SendAndValidateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogError("Postmark send failed with status {StatusCode}. Response: {Body}", (int)response.StatusCode, body);
        throw new InvalidOperationException($"Postmark email send failed with status {(int)response.StatusCode}.");
    }
}
