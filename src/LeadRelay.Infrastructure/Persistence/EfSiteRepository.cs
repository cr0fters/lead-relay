using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class EfSiteRepository(LeadRelayDbContext db) : ISiteRepository
{
    public async Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
    {
        var record = await db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == siteId, ct);
        if (record is null) return null;

        var connection = await db.WhatsAppConnections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == record.Id, ct);
        return Map(record, connection);
    }

    public async Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
    {
        var normalized = (phoneNumberId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var connection = await db.WhatsAppConnections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PhoneNumberId == normalized, ct);
        var record = connection is not null
            ? await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == connection.SiteId, ct)
            : await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == normalized, ct);
        if (record is null) return null;

        return Map(record, connection);
    }

    public async Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
    {
        var records = await db.Sites.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var connections = await db.WhatsAppConnections.AsNoTracking()
            .ToDictionaryAsync(x => x.SiteId, StringComparer.Ordinal, ct);

        return records.Select(record => Map(
            record,
            connections.TryGetValue(record.Id, out var connection) ? connection : null)).ToList();
    }

    public async Task UpsertAsync(Site site, CancellationToken ct)
    {
        var record = await db.Sites
            .FirstOrDefaultAsync(x => x.Id == site.Id, ct);

        if (record is null)
        {
            record = new SiteRecord
            {
                Id = site.Id
            };
            db.Sites.Add(record);
        }

        record.Name = site.Name;
        record.BusinessSummary = site.BusinessSummary;
        record.AllowedDomains = site.AllowedDomains.ToList();
        record.Fields = site.Fields.ToList();
        record.IntroMessage = site.IntroMessage;
        record.OwnerEmail = site.OwnerEmail;
        record.WhatsAppNumber = site.WhatsAppNumber;
        record.WhatsAppPhoneNumberId = site.WhatsAppPhoneNumberId;

        await db.SaveChangesAsync(ct);
    }

    private static Site Map(SiteRecord record, WhatsAppConnectionRecord? connection)
    {
        return new Site
        {
            Id = record.Id,
            Name = record.Name,
            BusinessSummary = record.BusinessSummary,
            AllowedDomains = record.AllowedDomains,
            Fields = record.Fields,
            IntroMessage = record.IntroMessage,
            OwnerEmail = record.OwnerEmail,
            WhatsAppNumber = connection?.DisplayPhoneNumber ?? record.WhatsAppNumber,
            WhatsAppPhoneNumberId = connection?.PhoneNumberId ?? record.WhatsAppPhoneNumberId
        };
    }
}
