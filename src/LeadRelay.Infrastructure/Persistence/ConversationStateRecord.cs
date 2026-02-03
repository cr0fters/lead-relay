namespace LeadRelay.Infrastructure.Persistence;

public sealed class ConversationStateRecord
{
    public string Id { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string WaId { get; set; } = "";
    public int StepIndex { get; set; }
    public Dictionary<string, string> Collected { get; set; } = new();
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? SessionStartedAtUtc { get; set; }
    public DateTimeOffset? LastActivityAtUtc { get; set; }
    public bool IsPaused { get; set; }
    public string? ContactName { get; set; }
    public List<ConversationTurnRecord> History { get; set; } = new();
    public string? SystemPromptOverride { get; set; }
    public Guid? LeadId { get; set; }
    public DateTimeOffset? LeadCreatedAtUtc { get; set; }
}

public sealed record ConversationTurnRecord(
    string Role,
    string Text,
    DateTimeOffset AtUtc);
