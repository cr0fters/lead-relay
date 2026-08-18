using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Web.Security;

public sealed class EfOwnerSessionVersionStore(LeadRelayDbContext db) : IOwnerSessionVersionStore
{
    public async Task<long?> GetAsync(string siteId, CancellationToken ct)
    {
        return await db.OwnerAccounts
            .AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .Select(x => (long?)x.SessionVersion)
            .FirstOrDefaultAsync(ct);
    }
}
