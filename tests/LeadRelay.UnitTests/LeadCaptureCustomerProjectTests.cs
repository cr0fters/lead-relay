using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Leads;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadCaptureCustomerProjectTests
{
    [Test]
    public async Task repeat_contact_creates_new_project_but_reuses_customer()
    {
        await using var db = CreateDb();
        var service = new LeadCaptureService(new InMemoryLeadRepository(), new NoOpEmailSender(), db);
        var site = BuildSite();

        var first = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000111",
                ContactName: "Jane",
                FallbackMessage: "Need help with kitchen",
                Fields: new Dictionary<string, string>
                {
                    ["project_summary"] = "Customer needs interior design for a full kitchen renovation.",
                    ["budget"] = "25000"
                },
                Conversation: new[] { new LeadCaptureTurn("user", "Need help with kitchen", DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        var second = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000111",
                ContactName: "Jane",
                FallbackMessage: "Need help with bathroom",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Need help with bathroom", DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        Assert.That(first.Lead, Is.Not.Null);
        Assert.That(second.Lead, Is.Not.Null);
        Assert.That(first.Lead!.CustomerId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(second.Lead!.CustomerId, Is.EqualTo(first.Lead.CustomerId));
        Assert.That(second.Lead.ProjectId, Is.Not.EqualTo(first.Lead.ProjectId));

        Assert.That(await db.Customers.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Projects.CountAsync(), Is.EqualTo(2));

        var firstProject = await db.Projects.FirstAsync(x => x.Id == first.Lead.ProjectId);
        Assert.That(firstProject.Summary, Is.EqualTo("Customer needs interior design for a full kitchen renovation."));
        Assert.That(firstProject.Fields["budget"], Is.EqualTo("25000"));
    }

    [Test]
    public async Task existing_lead_id_reuses_customer_and_project()
    {
        await using var db = CreateDb();
        var service = new LeadCaptureService(new InMemoryLeadRepository(), new NoOpEmailSender(), db);
        var site = BuildSite();

        var first = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000222",
                ContactName: "Alex",
                FallbackMessage: "First message",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "First message", DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        var second = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000222",
                ContactName: "Alex",
                FallbackMessage: "Second message",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Second message", DateTimeOffset.UtcNow) },
                LeadId: first.Lead!.Id,
                LeadCreatedAtUtc: first.Lead.CreatedAtUtc),
            CancellationToken.None);

        Assert.That(second.Lead, Is.Not.Null);
        Assert.That(second.Lead!.CustomerId, Is.EqualTo(first.Lead!.CustomerId));
        Assert.That(second.Lead.ProjectId, Is.EqualTo(first.Lead.ProjectId));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"lead-capture-tests-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }

    private static Site BuildSite()
    {
        return new Site
        {
            Id = "site_demo",
            Name = "Demo",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        };
    }

    private sealed class NoOpEmailSender : LeadRelay.Application.Abstractions.IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
            => Task.CompletedTask;
    }
}
