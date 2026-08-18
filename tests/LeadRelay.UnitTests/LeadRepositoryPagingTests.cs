using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
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
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = now.AddMinutes(-3),
            Name = "Alice",
            Email = "alice@example.com",
            ProjectStage = ProjectStatuses.Qualified,
            OwnerViewedAtUtc = now.AddMinutes(-2)
        }, CancellationToken.None);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = now.AddMinutes(-2),
            Name = "Bob",
            Email = "bob@example.com"
        }, CancellationToken.None);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = now.AddMinutes(-1),
            Name = "Charlie",
            Email = "charlie@example.com"
        }, CancellationToken.None);

        await repo.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = $"other_{Guid.NewGuid():N}",
            CreatedAtUtc = now,
            Name = "Other tenant"
        }, CancellationToken.None);

        var page1 = await repo.SearchBySiteAsync(siteId, Criteria(page: 1, pageSize: 2), CancellationToken.None);
        var page2 = await repo.SearchBySiteAsync(siteId, Criteria(page: 2, pageSize: 2), CancellationToken.None);
        var filtered = await repo.SearchBySiteAsync(siteId, Criteria(query: "bob"), CancellationToken.None);
        var stageFiltered = await repo.SearchBySiteAsync(siteId, Criteria(stage: ProjectStatuses.Qualified), CancellationToken.None);
        var dateFiltered = await repo.SearchBySiteAsync(
            siteId,
            Criteria(createdFromUtc: now.AddMinutes(-2.5)),
            CancellationToken.None);
        var exported = await repo.GetExportBySiteAsync(siteId, CancellationToken.None);

        Assert.That(page1.TotalCount, Is.EqualTo(3));
        Assert.That(page1.NewCount, Is.EqualTo(2));
        Assert.That(page1.Items.Count, Is.EqualTo(2));
        Assert.That(page1.Items[0].IsNew, Is.True);
        Assert.That(page2.Items.Count, Is.EqualTo(1));

        Assert.That(filtered.TotalCount, Is.EqualTo(1));
        Assert.That(filtered.Items[0].Name, Is.EqualTo("Bob"));
        Assert.That(stageFiltered.TotalCount, Is.EqualTo(1));
        Assert.That(stageFiltered.Items[0].Name, Is.EqualTo("Alice"));
        Assert.That(dateFiltered.TotalCount, Is.EqualTo(2));
        Assert.That(exported, Has.Count.EqualTo(3));
        Assert.That(exported.Any(x => x.Name == "Other tenant"), Is.False);
    }

    private static LeadSearchCriteria Criteria(
        string? query = null,
        string? stage = null,
        DateTimeOffset? createdFromUtc = null,
        DateTimeOffset? createdBeforeUtc = null,
        int page = 1,
        int pageSize = 20)
        => new(query, stage, createdFromUtc, createdBeforeUtc, page, pageSize);
}
