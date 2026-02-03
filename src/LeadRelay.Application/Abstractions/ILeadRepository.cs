using LeadRelay.Domain.Leads;

namespace LeadRelay.Application.Abstractions;

public interface ILeadRepository
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct);
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct);
}
