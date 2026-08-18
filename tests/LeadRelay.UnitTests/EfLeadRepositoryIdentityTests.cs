using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
using LeadRelay.Application.Abstractions;
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

    [Test]
    public async Task stage_update_is_tenant_scoped_and_records_each_actual_change_once()
    {
        await using var db = CreateDb();
        var repository = new EfLeadRepository(db);
        var siteId = "site_demo";
        var otherSiteId = "site_other";
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var changedAt = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        db.Sites.AddRange(
            new SiteRecord { Id = siteId, Name = "Demo", OwnerEmail = "owner@example.com", WhatsAppNumber = "447000000000" },
            new SiteRecord { Id = otherSiteId, Name = "Other", OwnerEmail = "other@example.com", WhatsAppNumber = "447000000001" });
        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = siteId,
            CreatedAtUtc = changedAt,
            UpdatedAtUtc = changedAt
        });
        db.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            SiteId = siteId,
            CustomerId = customerId,
            Name = "Project",
            Status = ProjectStatuses.New,
            CreatedAtUtc = changedAt,
            UpdatedAtUtc = changedAt
        });
        db.Leads.Add(new LeadRecord
        {
            Id = leadId,
            SiteId = siteId,
            CustomerId = customerId,
            ProjectId = projectId,
            Status = LeadStatuses.Open,
            CreatedAtUtc = changedAt
        });
        await db.SaveChangesAsync();

        var crossTenant = await repository.UpdateProjectStageAsync(
            leadId, otherSiteId, ProjectStatuses.Won, changedAt.AddMinutes(1), CancellationToken.None);
        var firstUpdate = await repository.UpdateProjectStageAsync(
            leadId, siteId, ProjectStatuses.Qualified, changedAt.AddMinutes(2), CancellationToken.None);
        var duplicateUpdate = await repository.UpdateProjectStageAsync(
            leadId, siteId, ProjectStatuses.Qualified, changedAt.AddMinutes(3), CancellationToken.None);

        Assert.That(crossTenant, Is.False);
        Assert.That(firstUpdate, Is.True);
        Assert.That(duplicateUpdate, Is.True);
        db.ChangeTracker.Clear();
        var project = await db.Projects.SingleAsync(x => x.Id == projectId);
        Assert.That(project.Status, Is.EqualTo(ProjectStatuses.Qualified));
        Assert.That(project.StageChanges, Has.Count.EqualTo(1));
        Assert.That(project.StageChanges[0].FromStage, Is.EqualTo(ProjectStatuses.New));
        Assert.That(project.StageChanges[0].ToStage, Is.EqualTo(ProjectStatuses.Qualified));

        var lead = await repository.GetByIdForSiteAsync(leadId, siteId, CancellationToken.None);
        Assert.That(lead?.ProjectStage, Is.EqualTo(ProjectStatuses.Qualified));
        Assert.That(lead?.ProjectStageChanges, Has.Count.EqualTo(1));

        var filtered = await repository.SearchBySiteAsync(
            siteId,
            new LeadSearchCriteria(
                Query: null,
                ProjectStage: ProjectStatuses.Qualified,
                CreatedFromUtc: changedAt.AddDays(-1),
                CreatedBeforeUtc: changedAt.AddDays(1),
                Page: 1,
                PageSize: 20),
            CancellationToken.None);
        Assert.That(filtered.TotalCount, Is.EqualTo(1));
        Assert.That(filtered.Items[0].ProjectStage, Is.EqualTo(ProjectStatuses.Qualified));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"ef-lead-repo-tests-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }
}
