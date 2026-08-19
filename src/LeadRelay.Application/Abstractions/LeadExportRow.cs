namespace LeadRelay.Application.Abstractions;

public sealed record LeadExportRow(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string? Name,
    string? Email,
    string? Phone,
    string Channel,
    bool IsTest,
    string ProjectStage,
    string? ProjectSummary,
    string? OwnerNotes,
    string? NextAction,
    DateTimeOffset? NextActionAtUtc,
    IReadOnlyDictionary<string, string> Fields);
