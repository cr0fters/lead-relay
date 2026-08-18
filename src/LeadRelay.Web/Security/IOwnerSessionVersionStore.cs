namespace LeadRelay.Web.Security;

public interface IOwnerSessionVersionStore
{
    Task<long?> GetAsync(string siteId, CancellationToken ct);
}
