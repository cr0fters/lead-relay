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
                    ["budget"] = "25000"
                },
                ProjectSummary: "Customer needs interior design for a full kitchen renovation.",
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
        Assert.That(firstProject.Fields.ContainsKey("project_summary"), Is.False);
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

    [Test]
    public async Task greeting_only_message_does_not_set_summary_and_project_overview_can_set_it_later()
    {
        await using var db = CreateDb();
        var service = new LeadCaptureService(new InMemoryLeadRepository(), new NoOpEmailSender(), db);
        var site = BuildSite();

        var first = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000333",
                ContactName: "Morgan",
                FallbackMessage: "hi",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "hi", DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        Assert.That(first.Lead, Is.Not.Null);
        var firstLead = first.Lead!;
        var firstProject = await db.Projects.FirstAsync(x => x.Id == firstLead.ProjectId);
        Assert.That(firstProject.Summary, Is.Null);

        await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000333",
                ContactName: "Morgan",
                FallbackMessage: "I'm looking for a bathroom redesign",
                Fields: new Dictionary<string, string>
                {
                    ["project_overview"] = "Bathroom redesign"
                },
                Conversation: new[] { new LeadCaptureTurn("user", "I'm looking for a bathroom redesign", DateTimeOffset.UtcNow) },
                LeadId: firstLead.Id,
                LeadCreatedAtUtc: firstLead.CreatedAtUtc),
            CancellationToken.None);

        var updatedProject = await db.Projects.FirstAsync(x => x.Id == firstLead.ProjectId);
        Assert.That(updatedProject.Summary, Is.EqualTo("Bathroom redesign"));
    }

    [Test]
    public async Task existing_paused_lead_stays_paused_when_capture_updates_same_lead()
    {
        await using var db = CreateDb();
        var repository = new InMemoryLeadRepository();
        var service = new LeadCaptureService(repository, new NoOpEmailSender(), db);
        var site = BuildSite();

        var first = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000444",
                ContactName: "Taylor",
                FallbackMessage: "Need help",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Need help", DateTimeOffset.UtcNow) }),
            CancellationToken.None);

        Assert.That(first.Lead, Is.Not.Null);
        var leadId = first.Lead!.Id;
        var pausedLead = await repository.GetByIdAsync(leadId, CancellationToken.None);
        Assert.That(pausedLead, Is.Not.Null);
        pausedLead!.IsBotPaused = true;
        await repository.SaveAsync(pausedLead, CancellationToken.None);

        var second = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000444",
                ContactName: "Taylor",
                FallbackMessage: "Timeline is ASAP",
                Fields: new Dictionary<string, string> { ["timeline"] = "ASAP" },
                Conversation: new[] { new LeadCaptureTurn("user", "Timeline is ASAP", DateTimeOffset.UtcNow) },
                LeadId: leadId,
                LeadCreatedAtUtc: first.Lead.CreatedAtUtc),
            CancellationToken.None);

        Assert.That(second.Lead, Is.Not.Null);
        Assert.That(second.Lead!.IsBotPaused, Is.True);
    }

    [Test]
    public async Task test_attribution_is_preserved_for_the_whole_conversation()
    {
        await using var db = CreateDb();
        var repository = new InMemoryLeadRepository();
        var service = new LeadCaptureService(repository, new NoOpEmailSender(), db);
        var site = BuildSite();

        var first = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000555",
                ContactName: "Setup tester",
                FallbackMessage: "Testing setup",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Testing setup", DateTimeOffset.UtcNow) },
                IsTest: false),
            CancellationToken.None);

        var updated = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000555",
                ContactName: "Setup tester",
                FallbackMessage: "Second message",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Second message", DateTimeOffset.UtcNow) },
                LeadId: first.Lead!.Id,
                LeadCreatedAtUtc: first.Lead.CreatedAtUtc,
                IsTest: true),
            CancellationToken.None);

        Assert.That(first.Lead.IsTest, Is.False);
        Assert.That(updated.Lead!.IsTest, Is.True);

        var preserved = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447000000555",
                ContactName: "Setup tester",
                FallbackMessage: "Another test message",
                Fields: new Dictionary<string, string>(),
                Conversation: new[] { new LeadCaptureTurn("user", "Another test message", DateTimeOffset.UtcNow) },
                LeadId: updated.Lead.Id,
                LeadCreatedAtUtc: updated.Lead.CreatedAtUtc,
                IsTest: false),
            CancellationToken.None);

        Assert.That(preserved.Lead!.IsTest, Is.True);
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
