using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.AI;

public sealed class OpenAIClient(HttpClient http, IOptions<OpenAIOptions> options, ILogger<OpenAIClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string?> CreateJsonResponseAsync(object payload, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("OpenAI request skipped: OpenAI:ApiKey is not configured.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            logger.LogWarning("OpenAI request skipped: OpenAI:Model is not configured.");
            return null;
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://api.openai.com/v1"
            : settings.BaseUrl.TrimEnd('/');

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI response failed with status {StatusCode}.", (int)response.StatusCode);
            return null;
        }

        return ExtractOutputText(body);
    }

    private static string? ExtractOutputText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            return outputText.GetString();

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var type) || type.GetString() != "output_text")
                    continue;

                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }

        return null;
    }
}
