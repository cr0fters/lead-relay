using LeadRelay.Domain.Leads;
using LeadRelay.Infrastructure.Persistence;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadRepositoryPagingTests
{
    [Test]
    public async Task search_by_site_filters_and_pages_results()
    {
        var repo = new InMemoryLeadRepository();
        var siteId = $"site_{Guid.NewGuid():N}";

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            Name = "Alice",
            Email = "alice@example.com"
        }, CancellationToken.None);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            Name = "Bob",
            Email = "bob@example.com"
        }, CancellationToken.None);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Name = "Charlie",
            Email = "charlie@example.com"
        }, CancellationToken.None);

        var page1 = await repo.SearchBySiteAsync(siteId, "", page: 1, pageSize: 2, CancellationToken.None);
        var page2 = await repo.SearchBySiteAsync(siteId, "", page: 2, pageSize: 2, CancellationToken.None);
        var filtered = await repo.SearchBySiteAsync(siteId, "bob", page: 1, pageSize: 20, CancellationToken.None);

        Assert.That(page1.TotalCount, Is.EqualTo(3));
        Assert.That(page1.Items.Count, Is.EqualTo(2));
        Assert.That(page2.Items.Count, Is.EqualTo(1));

        Assert.That(filtered.TotalCount, Is.EqualTo(1));
        Assert.That(filtered.Items[0].Name, Is.EqualTo("Bob"));
    }
}
