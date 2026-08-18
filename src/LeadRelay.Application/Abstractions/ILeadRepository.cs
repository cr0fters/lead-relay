using LeadRelay.Domain.Leads;

namespace LeadRelay.Application.Abstractions;

public interface ILeadRepository
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<LeadSummary>> GetRecentBySiteAsync(string siteId, int limit, CancellationToken ct);
    Task<LeadPageResult> SearchBySiteAsync(string siteId, LeadSearchCriteria criteria, CancellationToken ct);
    Task<IReadOnlyList<LeadExportRow>> GetExportBySiteAsync(string siteId, CancellationToken ct);
    Task<bool> UpdateProjectStageAsync(Guid leadId, string siteId, string stage, DateTimeOffset changedAtUtc, CancellationToken ct);
    Task<bool> UpdateProjectFollowUpAsync(
        Guid leadId,
        string siteId,
        string? ownerNotes,
        string? nextAction,
        DateTimeOffset? nextActionAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct);
    Task<bool> MarkViewedAsync(Guid leadId, string siteId, DateTimeOffset viewedAtUtc, CancellationToken ct);
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct);
}
