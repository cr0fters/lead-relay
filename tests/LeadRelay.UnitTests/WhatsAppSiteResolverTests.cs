using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppSiteResolverTests
{
    [Test]
    public async Task unknown_phone_number_id_does_not_fall_back_to_display_number()
    {
        var matchingDisplaySite = CreateSite("site_a", "known-id", "447000000000");
        var resolver = CreateResolver([matchingDisplaySite]);

        var result = await resolver.ResolveAsync("unknown-id", "+44 7000 000000", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task display_number_fallback_requires_exactly_one_match()
    {
        var resolver = CreateResolver(
        [
            CreateSite("site_a", null, "447000000000"),
            CreateSite("site_b", null, "+44 7000 000000")
        ]);

        var result = await resolver.ResolveAsync(null, "447000000000", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    private static WhatsAppSiteResolver CreateResolver(IReadOnlyList<Site> sites)
        => new(new SiteRepository(sites), NullLogger<WhatsAppSiteResolver>.Instance);

    private static Site CreateSite(string id, string? phoneNumberId, string displayNumber) => new()
    {
        Id = id,
        Name = id,
        OwnerEmail = $"{id}@example.com",
        WhatsAppNumber = displayNumber,
        WhatsAppPhoneNumberId = phoneNumberId
    };

    private sealed class SiteRepository(IReadOnlyList<Site> sites) : ISiteRepository
    {
        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
            => Task.FromResult(sites.FirstOrDefault(x => x.Id == siteId));

        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult(sites.FirstOrDefault(x => x.WhatsAppPhoneNumberId == phoneNumberId));

        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct) => Task.FromResult(sites);

        public Task UpsertAsync(Site site, CancellationToken ct) => throw new NotSupportedException();
    }
}
