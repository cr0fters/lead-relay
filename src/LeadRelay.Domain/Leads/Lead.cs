using LeadRelay.Domain.Projects;

namespace LeadRelay.Domain.Leads;

public sealed class Lead
{
    public required Guid Id { get; init; }
    public required string SiteId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? OwnerViewedAtUtc { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProjectId { get; set; }
    public string Channel { get; set; } = "api";
    public string Status { get; set; } = LeadStatuses.Open;
    public bool IsBotPaused { get; set; }
    public string ProjectStage { get; set; } = ProjectStatuses.New;
    public string? ProjectSummary { get; set; }
    public string? OwnerNotes { get; set; }
    public string? NextAction { get; set; }
    public DateTimeOffset? NextActionAtUtc { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Dictionary<string, string> Utm { get; init; } = new();
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProjectStageChange> ProjectStageChanges { get; init; } = new();
    public List<LeadConversationTurn> Conversation { get; init; } = new();
}

public sealed record LeadConversationTurn(
    string Role,
    string Text,
    DateTimeOffset AtUtc);
