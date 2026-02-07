using LeadRelay.Domain.Leads;

namespace LeadRelay.Application.Abstractions;

public interface ILeadRepository
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<LeadSummary>> GetRecentBySiteAsync(string siteId, int limit, CancellationToken ct);
    Task<LeadPageResult> SearchBySiteAsync(string siteId, string? query, int page, int pageSize, CancellationToken ct);
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct);
}
