namespace LeadRelay.Domain.Conversations;

public sealed class ConversationSession
{
    public required Guid Id { get; init; }
    public required string SiteId { get; init; }
    public required string WaId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public required string State { get; set; }
    public string? LeadId { get; set; }
    public string? LastUserMessage { get; set; }
}
