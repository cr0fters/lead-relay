using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using System.Collections.Concurrent;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class InMemoryLeadRepository : ILeadRepository
{
    static readonly ConcurrentDictionary<Guid, Lead> Store = new();

    public Task SaveAsync(Lead lead, CancellationToken ct)
    {
        Store[lead.Id] = lead;
        return Task.CompletedTask;
    }
}
