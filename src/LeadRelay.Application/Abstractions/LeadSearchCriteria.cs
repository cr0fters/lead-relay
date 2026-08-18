namespace LeadRelay.Application.Abstractions;

public sealed record LeadSearchCriteria(
    string? Query,
    string? ProjectStage,
    DateTimeOffset? CreatedFromUtc,
    DateTimeOffset? CreatedBeforeUtc,
    int Page,
    int PageSize);
