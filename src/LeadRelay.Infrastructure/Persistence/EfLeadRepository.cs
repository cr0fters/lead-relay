using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class EfLeadRepository(LeadRelayDbContext db) : ILeadRepository
{
    public async Task SaveAsync(Lead lead, CancellationToken ct)
    {
        var record = await db.Leads.FirstOrDefaultAsync(x => x.Id == lead.Id, ct);
        if (record is null && !string.IsNullOrWhiteSpace(lead.Phone))
        {
            record = await db.Leads.FirstOrDefaultAsync(
                x => x.SiteId == lead.SiteId && x.Phone == lead.Phone,
                ct);
        }

        if (record is null && !string.IsNullOrWhiteSpace(lead.Email))
        {
            record = await db.Leads.FirstOrDefaultAsync(
                x => x.SiteId == lead.SiteId && x.Email == lead.Email,
                ct);
        }
        if (record is null)
        {
            record = new LeadRecord { Id = lead.Id };
            db.Leads.Add(record);
        }

        record.SiteId = lead.SiteId;
        record.CreatedAtUtc = lead.CreatedAtUtc;
        record.Name = lead.Name;
        record.Email = lead.Email;
        record.Phone = lead.Phone;
        record.Intent = lead.Intent;
        record.Notes = lead.Notes;
        record.PageUrl = lead.PageUrl;
        record.Referrer = lead.Referrer;
        record.Utm = lead.Utm;
        record.Fields = lead.Fields;
        record.Conversation = lead.Conversation;

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, 200);
        return await db.Leads.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
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
        return await db.Leads.AsNoTracking()
            .Where(x => x.SiteId == normalizedSiteId)
            .OrderByDescending(x => x.CreatedAtUtc)
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

    public async Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var record = await db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return record is null ? null : Map(record);
    }

    public async Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct)
    {
        var normalizedSiteId = (siteId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSiteId)) return null;

        var record = await db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.SiteId == normalizedSiteId, ct);
        return record is null ? null : Map(record);
    }

    private static Lead Map(LeadRecord record)
    {
        var lead = new Lead
        {
            Id = record.Id,
            SiteId = record.SiteId,
            CreatedAtUtc = record.CreatedAtUtc,
            Name = record.Name,
            Email = record.Email,
            Phone = record.Phone,
            Intent = record.Intent,
            Notes = record.Notes,
            PageUrl = record.PageUrl,
            Referrer = record.Referrer,
            Utm = new Dictionary<string, string>(record.Utm, StringComparer.OrdinalIgnoreCase),
            Fields = new Dictionary<string, string>(record.Fields, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var turn in record.Conversation)
            lead.Conversation.Add(turn);

        return lead;
    }
}
