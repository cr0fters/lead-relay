using LeadRelay.Domain.Leads;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class EfLeadRepositoryIdentityTests
{
    [Test]
    public async Task save_does_not_merge_different_lead_ids_with_same_phone_or_email()
    {
        await using var db = CreateDb();
        var repository = new EfLeadRepository(db);
        var siteId = "site_demo";

        await repository.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Phone = "447000000000",
            Email = "same@example.com"
        }, CancellationToken.None);

        await repository.SaveAsync(new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Phone = "447000000000",
            Email = "same@example.com"
        }, CancellationToken.None);

        var count = await db.Leads.CountAsync();
        Assert.That(count, Is.EqualTo(2));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"ef-lead-repo-tests-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }
}
