using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Leads;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.IntegrationTests;

public sealed class LeadCaptureIntegrationTests
{
    [Test]
    public async Task capture_persists_customer_project_and_lead_with_expected_relationships()
    {
        await using var db = CreateDb();
        var repository = new EfLeadRepository(db);
        var emailSender = new RecordingEmailSender();
        var service = new LeadCaptureService(repository, emailSender, db);
        var site = CreateSite();
        var now = DateTimeOffset.UtcNow;

        var result = await service.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: "447700900123",
                ContactName: "Casey Morgan",
                FallbackMessage: "Need help designing our office fit-out",
                Fields: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["project_overview"] = "Office fit-out for 30 desks",
                    ["timeline"] = "Q3"
                },
                Conversation:
                [
                    new LeadCaptureTurn("user", "Need help designing our office fit-out", now)
                ],
                LeadId: null,
                LeadCreatedAtUtc: now,
                NotifyOwner: true,
                ProjectSummary: "Office fit-out project"),
            CancellationToken.None);

        Assert.That(result.Saved, Is.True);
        Assert.That(result.Lead, Is.Not.Null);
        var leadModel = result.Lead!;
        Assert.That(leadModel.CustomerId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(leadModel.ProjectId, Is.Not.EqualTo(Guid.Empty));

        var customer = await db.Customers.SingleAsync(x => x.Id == leadModel.CustomerId);
        var project = await db.Projects.SingleAsync(x => x.Id == leadModel.ProjectId);
        var lead = await db.Leads.SingleAsync(x => x.Id == leadModel.Id);

        Assert.Multiple(() =>
        {
            Assert.That(customer.Name, Is.EqualTo("Casey Morgan"));
            Assert.That(customer.Phone, Is.EqualTo("447700900123"));
            Assert.That(project.CustomerId, Is.EqualTo(customer.Id));
            Assert.That(project.Fields["timeline"], Is.EqualTo("Q3"));
            Assert.That(lead.CustomerId, Is.EqualTo(customer.Id));
            Assert.That(lead.ProjectId, Is.EqualTo(project.Id));
            Assert.That(emailSender.Sent.Count, Is.EqualTo(1));
        });
    }

    private static Site CreateSite()
    {
        return new Site
        {
            Id = "site_integration",
            Name = "Integration Site",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447700900123",
            AllowedDomains = ["localhost"],
            IntroMessage = "Hello",
            Fields =
            [
                new ConversationField { Id = "project_overview", Name = "Project overview" },
                new ConversationField { Id = "timeline", Name = "Timeline" }
            ]
        };
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"lead_capture_integration_{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private sealed class RecordingEmailSender : LeadRelay.Application.Abstractions.IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
