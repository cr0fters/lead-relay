using LeadRelay.Domain.Leads;

namespace LeadRelay.Application.Abstractions;

public interface ILeadRepository
{
    Task SaveAsync(Lead lead, CancellationToken ct);
}
