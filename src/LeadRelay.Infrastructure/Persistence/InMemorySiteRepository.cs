using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class InMemorySiteRepository : ISiteRepository
{
    private static readonly Site[] Sites =
    [
        new()
        {
            Id = "site_demo",
            Name = "Demo site",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            AllowedDomains = ["localhost"]
        }
    ];

    public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct) => Task.FromResult(Sites.FirstOrDefault(x => x.Id == siteId));
}
