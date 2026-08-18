namespace LeadRelay.Application.Abstractions;

public sealed record LeadSummary(
    Guid Id,
    string SiteId,
    string? Name,
    string? Phone,
    string? Email,
    DateTimeOffset CreatedAtUtc,
    string ProjectStage);
