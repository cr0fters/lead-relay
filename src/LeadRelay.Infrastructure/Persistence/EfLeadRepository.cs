using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
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

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, 200);
        var query =
            from lead in db.Leads.AsNoTracking()
            join customer in db.Customers.AsNoTracking()
                on lead.CustomerId equals customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            orderby lead.CreatedAtUtc descending
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Phone = customer != null ? customer.Phone : null,
                Email = customer != null ? customer.Email : null
            };

        return await query
            .Take(take)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc))
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
                on lead.CustomerId equals customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            where lead.SiteId == normalizedSiteId
            orderby lead.CreatedAtUtc descending
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Phone = customer != null ? customer.Phone : null,
                Email = customer != null ? customer.Email : null
            };

        return await query
            .Take(take)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<LeadPageResult> SearchBySiteAsync(string siteId, string? query, int page, int pageSize, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId))
            return new LeadPageResult(Array.Empty<LeadSummary>(), 0, 1, Math.Clamp(pageSize, 1, 100));

        var normalizedQuery = (query ?? "").Trim();
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);
        var effectivePage = Math.Max(1, page);

        var baseQuery =
            from lead in db.Leads.AsNoTracking()
            join customer in db.Customers.AsNoTracking()
                on lead.CustomerId equals customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            where lead.SiteId == normalizedSiteId
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                Name = customer != null ? customer.Name : null,
                Email = customer != null ? customer.Email : null,
                Phone = customer != null ? customer.Phone : null
            };

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var pattern = $"%{normalizedQuery}%";
            baseQuery = baseQuery.Where(x =>
                (x.Name != null && EF.Functions.Like(x.Name, pattern)) ||
                (x.Email != null && EF.Functions.Like(x.Email, pattern)) ||
                (x.Phone != null && EF.Functions.Like(x.Phone, pattern)));
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(x => new LeadSummary(
                x.Id,
                x.SiteId,
                x.Name,
                x.Phone,
                x.Email,
                x.CreatedAtUtc))
            .ToListAsync(ct);

        return new LeadPageResult(items, totalCount, effectivePage, effectivePageSize);
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
