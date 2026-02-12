using LeadRelay.Domain.Sites;

namespace LeadRelay.Application.Abstractions;

public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(string siteId, CancellationToken ct);
    Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct);
    Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct);
    Task UpsertAsync(Site site, CancellationToken ct);
}
