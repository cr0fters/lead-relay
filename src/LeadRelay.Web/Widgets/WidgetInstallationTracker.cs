using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Web.Widgets;

public sealed class WidgetInstallationTracker(
    LeadRelayDbContext db,
    IClock clock,
    ILogger<WidgetInstallationTracker> logger) : IWidgetInstallationTracker
{
    public async Task RecordSuccessfulLoadAsync(string siteId, string domain, CancellationToken ct)
    {
        var account = await db.OwnerAccounts.SingleOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (account is null ||
            (account.WidgetInstalledAtUtc.HasValue &&
             string.Equals(account.WidgetInstalledDomain, domain, StringComparison.OrdinalIgnoreCase)))
            return;

        var now = clock.UtcNow;
        account.WidgetInstalledAtUtc = now;
        account.WidgetInstalledDomain = domain;
        account.UpdatedAtUtc = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Could not record widget installation for site {SiteId}.", siteId);
        }
    }
}
