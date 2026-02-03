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
}
