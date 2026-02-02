using LeadRelay.Domain.Sites;

namespace LeadRelay.Application.Abstractions;

public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(string siteId, CancellationToken ct);
}
