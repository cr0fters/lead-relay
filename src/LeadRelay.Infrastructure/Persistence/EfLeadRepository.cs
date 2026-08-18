using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class EfLeadRepository(LeadRelayDbContext db) : ILeadRepository
{
    public async Task SaveAsync(Lead lead, CancellationToken ct)
    {
        var record = await db.Leads.FirstOrDefaultAsync(x => x.Id == lead.Id, ct);
        if (record is null)
        {
            record = new LeadRecord { Id = lead.Id };
            db.Leads.Add(record);
        }

        record.SiteId = lead.SiteId;
        record.CreatedAtUtc = lead.CreatedAtUtc;
        record.CustomerId = lead.CustomerId;
        record.ProjectId = lead.ProjectId;
        record.Channel = NormalizeChannel(lead.Channel);
        record.Status = NormalizeStatus(lead.Status);
        record.IsBotPaused = lead.IsBotPaused;
        record.Utm = lead.Utm;
        record.Conversation = lead.Conversation;

        var customer = await db.Customers.FirstOrDefaultAsync(
            x => x.SiteId == record.SiteId && x.Id == record.CustomerId,
            ct);
        if (customer is not null)
        {
            customer.Name = NormalizeText(lead.Name) ?? customer.Name;
            customer.Email = NormalizeText(lead.Email) ?? customer.Email;
            customer.Phone = NormalizeText(lead.Phone) ?? customer.Phone;
            customer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var project = await db.Projects.FirstOrDefaultAsync(
            x => x.SiteId == record.SiteId && x.Id == record.ProjectId,
            ct);
        if (project is not null)
        {
            foreach (var pair in lead.Fields)
            {
                var key = (pair.Key ?? "").Trim();
                var value = (pair.Value ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;

                project.Fields[key] = value;
            }
            if (!string.IsNullOrWhiteSpace(lead.ProjectSummary))
                project.Summary = lead.ProjectSummary.Trim();
            project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, 200);
        var query =
            from lead in db.Leads.AsNoTracking()
            join customer in db.Customers.AsNoTracking()
                on new { lead.SiteId, Id = lead.CustomerId }
                equals new { customer.SiteId, customer.Id } into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join project in db.Projects.AsNoTracking()
                on new { lead.SiteId, Id = lead.ProjectId }
                equals new { project.SiteId, project.Id }
            orderby lead.CreatedAtUtc descending
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Phone = customer != null ? customer.Phone : null,
                Email = customer != null ? customer.Email : null,
                ProjectStage = project.Status
            };

        return await query
            .Take(take)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc,
                x.ProjectStage))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LeadSummary>> GetRecentBySiteAsync(string siteId, int limit, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId)) return Array.Empty<LeadSummary>();

        var take = Math.Clamp(limit, 1, 200);
        var query =
            from lead in db.Leads.AsNoTracking()
            join customer in db.Customers.AsNoTracking()
                on new { lead.SiteId, Id = lead.CustomerId }
                equals new { customer.SiteId, customer.Id } into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join project in db.Projects.AsNoTracking()
                on new { lead.SiteId, Id = lead.ProjectId }
                equals new { project.SiteId, project.Id }
            where lead.SiteId == normalizedSiteId
            orderby lead.CreatedAtUtc descending
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Phone = customer != null ? customer.Phone : null,
                Email = customer != null ? customer.Email : null,
                ProjectStage = project.Status
            };

        return await query
            .Take(take)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc,
                x.ProjectStage))
            .ToListAsync(ct);
    }

    public async Task<LeadPageResult> SearchBySiteAsync(string siteId, LeadSearchCriteria criteria, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId))
            return new LeadPageResult(Array.Empty<LeadSummary>(), 0, 1, Math.Clamp(criteria.PageSize, 1, 100));

        var normalizedQuery = (criteria.Query ?? "").Trim();
        var normalizedStage = ProjectStatuses.IsOwnerStage(criteria.ProjectStage)
            ? criteria.ProjectStage!.Trim().ToLowerInvariant()
            : null;
        var effectivePageSize = Math.Clamp(criteria.PageSize, 1, 100);
        var effectivePage = Math.Max(1, criteria.Page);

        var baseQuery =
            from lead in db.Leads.AsNoTracking()
            join customer in db.Customers.AsNoTracking()
                on new { lead.SiteId, Id = lead.CustomerId }
                equals new { customer.SiteId, customer.Id } into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join project in db.Projects.AsNoTracking()
                on new { lead.SiteId, Id = lead.ProjectId }
                equals new { project.SiteId, project.Id }
            where lead.SiteId == normalizedSiteId
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Email = customer != null ? customer.Email : null,
                Phone = customer != null ? customer.Phone : null,
                ProjectStage = project.Status
            };

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var pattern = $"%{normalizedQuery}%";
            baseQuery = baseQuery.Where(x =>
                (x.Name != null && EF.Functions.Like(x.Name, pattern)) ||
                (x.Email != null && EF.Functions.Like(x.Email, pattern)) ||
                (x.Phone != null && EF.Functions.Like(x.Phone, pattern)));
        }

        if (normalizedStage is not null)
            baseQuery = baseQuery.Where(x => x.ProjectStage == normalizedStage);

        if (criteria.CreatedFromUtc is not null)
            baseQuery = baseQuery.Where(x => x.CreatedAtUtc >= criteria.CreatedFromUtc.Value);

        if (criteria.CreatedBeforeUtc is not null)
            baseQuery = baseQuery.Where(x => x.CreatedAtUtc < criteria.CreatedBeforeUtc.Value);

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc,
                x.ProjectStage))
            .ToListAsync(ct);

        return new LeadPageResult(items, totalCount, effectivePage, effectivePageSize);
    }

    public async Task<bool> UpdateProjectStageAsync(
        Guid leadId,
        string siteId,
        string stage,
        DateTimeOffset changedAtUtc,
        CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        var normalizedStage = (stage ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSiteId) || !ProjectStatuses.IsOwnerStage(normalizedStage))
            return false;

        var lead = await db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == leadId && x.SiteId == normalizedSiteId, ct);
        if (lead is null) return false;

        var project = await db.Projects
            .FirstOrDefaultAsync(x => x.Id == lead.ProjectId && x.SiteId == normalizedSiteId, ct);
        if (project is null) return false;

        var previousStage = ProjectStatuses.Normalize(project.Status);
        if (string.Equals(previousStage, normalizedStage, StringComparison.Ordinal))
            return true;

        project.Status = normalizedStage;
        project.StageChanges ??= new List<ProjectStageChange>();
        project.StageChanges.Add(new ProjectStageChange(previousStage, normalizedStage, changedAtUtc));
        project.UpdatedAtUtc = changedAtUtc;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateProjectFollowUpAsync(
        Guid leadId,
        string siteId,
        string? ownerNotes,
        string? nextAction,
        DateTimeOffset? nextActionAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId)) return false;

        var normalizedNotes = NormalizeText(ownerNotes);
        var normalizedAction = NormalizeText(nextAction);
        if (normalizedNotes?.Length > 4000 ||
            normalizedAction?.Length > 500 ||
            (normalizedAction is null && nextActionAtUtc is not null))
            return false;

        var lead = await db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == leadId && x.SiteId == normalizedSiteId, ct);
        if (lead is null) return false;

        var project = await db.Projects
            .FirstOrDefaultAsync(x => x.Id == lead.ProjectId && x.SiteId == normalizedSiteId, ct);
        if (project is null) return false;

        project.OwnerNotes = normalizedNotes;
        project.NextAction = normalizedAction;
        project.NextActionAtUtc = project.NextAction is null ? null : nextActionAtUtc;
        project.UpdatedAtUtc = updatedAtUtc;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var record = await db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (record is null) return null;

        var customer = await LoadCustomerAsync(record, ct);
        var project = await LoadProjectAsync(record, ct);
        return Map(record, customer, project);
    }

    public async Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId)) return null;

        var record = await db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.SiteId == normalizedSiteId, ct);
        if (record is null) return null;

        var customer = await LoadCustomerAsync(record, ct);
        var project = await LoadProjectAsync(record, ct);
        return Map(record, customer, project);
    }

    private static Lead Map(LeadRecord record, CustomerRecord? customer, ProjectRecord? project)
    {
        var lead = new Lead
        {
            Id = record.Id,
            SiteId = record.SiteId,
            CreatedAtUtc = record.CreatedAtUtc,
            CustomerId = record.CustomerId,
            ProjectId = record.ProjectId,
            Channel = NormalizeChannel(record.Channel),
            Status = NormalizeStatus(record.Status),
            IsBotPaused = record.IsBotPaused,
            ProjectStage = ProjectStatuses.Normalize(project?.Status),
            ProjectSummary = project?.Summary,
            OwnerNotes = project?.OwnerNotes,
            NextAction = project?.NextAction,
            NextActionAtUtc = project?.NextActionAtUtc,
            Name = customer?.Name,
            Email = customer?.Email,
            Phone = customer?.Phone,
            Utm = new Dictionary<string, string>(record.Utm, StringComparer.OrdinalIgnoreCase),
            Fields = project is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(project.Fields, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var turn in record.Conversation)
            lead.Conversation.Add(turn);

        if (project is not null)
        {
            foreach (var stageChange in project.StageChanges ?? [])
                lead.ProjectStageChanges.Add(stageChange);
        }

        return lead;
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = (status ?? "").Trim().ToLowerInvariant();
        return normalized is LeadStatuses.Open or LeadStatuses.Closed
            ? normalized
            : LeadStatuses.Open;
    }

    private static string NormalizeChannel(string? channel)
    {
        var normalized = (channel ?? "").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "api" : normalized;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = (value ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<CustomerRecord?> LoadCustomerAsync(LeadRecord record, CancellationToken ct)
    {
        return await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == record.SiteId && x.Id == record.CustomerId, ct);
    }

    private async Task<ProjectRecord?> LoadProjectAsync(LeadRecord record, CancellationToken ct)
    {
        return await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == record.SiteId && x.Id == record.ProjectId, ct);
    }
}
