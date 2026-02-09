using System.Text.Json.Serialization;

namespace LeadRelay.Domain.Sites;

public sealed class Site
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? BusinessSummary { get; init; }
    public IReadOnlyList<string> AllowedDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ConversationField> Fields { get; init; } = Array.Empty<ConversationField>();
    public string? IntroMessage { get; init; }
    public required string OwnerEmail { get; init; }
    public required string WhatsAppNumber { get; init; }
}

public sealed class ConversationField
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    [JsonPropertyName("key")]
    public string? LegacyKey
    {
        set
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = value?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(Name))
                Name = value?.Trim() ?? "";
        }
    }

    [JsonPropertyName("prompt")]
    public string? LegacyPrompt
    {
        set
        {
            if (string.IsNullOrWhiteSpace(Description))
                Description = value?.Trim();
        }
    }
}
