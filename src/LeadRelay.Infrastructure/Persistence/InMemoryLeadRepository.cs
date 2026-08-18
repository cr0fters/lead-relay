using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
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
                x.CreatedAtUtc,
                ProjectStatuses.Normalize(x.ProjectStage)))
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
                x.CreatedAtUtc,
                ProjectStatuses.Normalize(x.ProjectStage)))
            .ToList();

        return Task.FromResult<IReadOnlyList<LeadSummary>>(items);
    }

    public Task<LeadPageResult> SearchBySiteAsync(string siteId, LeadSearchCriteria criteria, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedQuery = (criteria.Query ?? "").Trim();
        var normalizedStage = ProjectStatuses.IsOwnerStage(criteria.ProjectStage)
            ? criteria.ProjectStage!.Trim().ToLowerInvariant()
            : null;
        var effectivePageSize = Math.Clamp(criteria.PageSize, 1, 100);
        var effectivePage = Math.Max(1, criteria.Page);

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

        if (normalizedStage is not null)
            filtered = filtered.Where(x => ProjectStatuses.Normalize(x.ProjectStage) == normalizedStage);

        if (criteria.CreatedFromUtc is not null)
            filtered = filtered.Where(x => x.CreatedAtUtc >= criteria.CreatedFromUtc.Value);

        if (criteria.CreatedBeforeUtc is not null)
            filtered = filtered.Where(x => x.CreatedAtUtc < criteria.CreatedBeforeUtc.Value);

        var ordered = filtered
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToList();
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
                x.CreatedAtUtc,
                ProjectStatuses.Normalize(x.ProjectStage)))
            .ToList();

        return Task.FromResult(new LeadPageResult(items, total, effectivePage, effectivePageSize));
    }

    public Task<bool> UpdateProjectStageAsync(
        Guid leadId,
        string siteId,
        string stage,
        DateTimeOffset changedAtUtc,
        CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedStage = (stage ?? "").Trim().ToLowerInvariant();
        if (!ProjectStatuses.IsOwnerStage(normalizedStage) ||
            !Store.TryGetValue(leadId, out var lead) ||
            !string.Equals(lead.SiteId, normalizedSiteId, StringComparison.Ordinal))
            return Task.FromResult(false);

        var previousStage = ProjectStatuses.Normalize(lead.ProjectStage);
        if (!string.Equals(previousStage, normalizedStage, StringComparison.Ordinal))
        {
            lead.ProjectStage = normalizedStage;
            lead.ProjectStageChanges.Add(new ProjectStageChange(previousStage, normalizedStage, changedAtUtc));
        }

        return Task.FromResult(true);
    }

    public Task<bool> UpdateProjectFollowUpAsync(
        Guid leadId,
        string siteId,
        string? ownerNotes,
        string? nextAction,
        DateTimeOffset? nextActionAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedNotes = NormalizeText(ownerNotes);
        var normalizedAction = NormalizeText(nextAction);
        if (normalizedNotes?.Length > 4000 ||
            normalizedAction?.Length > 500 ||
            (normalizedAction is null && nextActionAtUtc is not null))
            return Task.FromResult(false);
        if (!Store.TryGetValue(leadId, out var lead) ||
            !string.Equals(lead.SiteId, normalizedSiteId, StringComparison.Ordinal))
            return Task.FromResult(false);

        lead.OwnerNotes = normalizedNotes;
        lead.NextAction = normalizedAction;
        lead.NextActionAtUtc = lead.NextAction is null ? null : nextActionAtUtc;
        return Task.FromResult(true);
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

    private static string? NormalizeText(string? value)
    {
        var normalized = (value ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
