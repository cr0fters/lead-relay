namespace LeadRelay.Domain.Sites;

public sealed class Site
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? BusinessSummary { get; init; }
    public IReadOnlyList<string> AllowedDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ConversationField> Fields { get; init; } = Array.Empty<ConversationField>();
    public required string OwnerEmail { get; init; }
    public required string WhatsAppNumber { get; init; }
}

public sealed class ConversationField
{
    public required string Key { get; init; }
    public required string Prompt { get; init; }
    public bool Required { get; init; } = true;
    public ConversationFieldType Type { get; init; } = ConversationFieldType.Text;
}

public enum ConversationFieldType
{
    Text = 0,
    Email = 1,
    Phone = 2
}
