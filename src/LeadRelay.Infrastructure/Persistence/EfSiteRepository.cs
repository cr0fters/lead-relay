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

        return new Site
        {
            Id = record.Id,
            Name = record.Name,
            BusinessSummary = record.BusinessSummary,
            AllowedDomains = record.AllowedDomains,
            Fields = record.Fields,
            OptionalFields = record.OptionalFields,
            IntroMessage = record.IntroMessage,
            OwnerEmail = record.OwnerEmail,
            WhatsAppNumber = record.WhatsAppNumber
        };
    }
}
