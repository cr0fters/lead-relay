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

    [Test]
    public async Task save_with_empty_lead_fields_does_not_clear_existing_project_fields()
    {
        await using var db = CreateDb();
        var repository = new EfLeadRepository(db);
        var siteId = "site_demo";
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        db.Sites.Add(new SiteRecord
        {
            Id = siteId,
            Name = "Demo",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        });
        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            SiteId = siteId,
            CustomerId = customerId,
            Name = "Project",
            Status = "new",
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["budget"] = "25000"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Leads.Add(new LeadRecord
        {
            Id = leadId,
            SiteId = siteId,
            CustomerId = customerId,
            ProjectId = projectId,
            Status = LeadStatuses.Open,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await repository.SaveAsync(new Lead
        {
            Id = leadId,
            SiteId = siteId,
            CustomerId = customerId,
            ProjectId = projectId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = LeadStatuses.Open
        }, CancellationToken.None);

        var project = await db.Projects.FirstAsync(x => x.Id == projectId);
        Assert.That(project.Fields.TryGetValue("budget", out var value), Is.True);
        Assert.That(value, Is.EqualTo("25000"));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"ef-lead-repo-tests-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }
}
