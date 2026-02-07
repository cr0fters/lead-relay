namespace LeadRelay.Application.Abstractions;

public sealed record LeadPageResult(
    IReadOnlyList<LeadSummary> Items,
    int TotalCount,
    int Page,
    int PageSize);
