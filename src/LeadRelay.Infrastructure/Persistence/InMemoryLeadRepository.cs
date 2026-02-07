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

    public Task<IReadOnlyList<LeadSummary>> GetRecentBySiteAsync(string siteId, int limit, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId))
            return Task.FromResult<IReadOnlyList<LeadSummary>>(Array.Empty<LeadSummary>());

        var take = Math.Clamp(limit, 1, 200);
        var items = Store.Values
            .Where(x => string.Equals(x.SiteId, normalizedSiteId, StringComparison.Ordinal))
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

    public Task<LeadPageResult> SearchBySiteAsync(string siteId, string? query, int page, int pageSize, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedQuery = (query ?? "").Trim();
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);
        var effectivePage = Math.Max(1, page);

        if (string.IsNullOrWhiteSpace(normalizedSiteId))
            return Task.FromResult(new LeadPageResult(Array.Empty<LeadSummary>(), 0, effectivePage, effectivePageSize));

        var filtered = Store.Values
            .Where(x => string.Equals(x.SiteId, normalizedSiteId, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            filtered = filtered.Where(x =>
                Contains(x.Name, normalizedQuery) ||
                Contains(x.Email, normalizedQuery) ||
                Contains(x.Phone, normalizedQuery));
        }

        var ordered = filtered.OrderByDescending(x => x.CreatedAtUtc).ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc))
            .ToList();

        return Task.FromResult(new LeadPageResult(items, total, effectivePage, effectivePageSize));
    }

    public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        Store.TryGetValue(id, out var lead);
        return Task.FromResult<Lead?>(lead);
    }

    public Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct)
    {
        Store.TryGetValue(id, out var lead);
        if (lead is null) return Task.FromResult<Lead?>(null);

        var normalizedSiteId = (siteId ?? "").Trim();
        if (!string.Equals(lead.SiteId, normalizedSiteId, StringComparison.Ordinal))
            return Task.FromResult<Lead?>(null);

        return Task.FromResult<Lead?>(lead);
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
