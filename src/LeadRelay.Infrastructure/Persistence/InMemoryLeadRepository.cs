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

    public Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, 200);
        var items = Store.Values
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<LeadSummary>>(items);
    }

    public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        Store.TryGetValue(id, out var lead);
        return Task.FromResult<Lead?>(lead);
    }
}
