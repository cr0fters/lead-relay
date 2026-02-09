using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.AI;
using LeadRelay.Web.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppLeadSelectionTests
{
    [Test]
    public async Task reuses_recent_open_lead_for_same_customer()
    {
        var now = new DateTimeOffset(2026, 2, 9, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDb();

        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = "site_demo",
            ExternalContactId = "447000000333",
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        db.Leads.Add(new LeadRecord
        {
            Id = leadId,
            SiteId = "site_demo",
            CustomerId = customerId,
            Status = LeadStatuses.Open,
            CreatedAtUtc = now.AddHours(-2)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, now, reuseWindowHours: 72);
        var site = BuildSite();

        var reply = await service.HandleMessageAsync(site, "447000000333", "Hi", "Jane", null, CancellationToken.None);

        Assert.That(reply.LeadId, Is.EqualTo(leadId));
        Assert.That(reply.LeadJustCreated, Is.False);
    }

    [Test]
    public async Task creates_new_lead_when_only_open_lead_is_stale()
    {
        var now = new DateTimeOffset(2026, 2, 9, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDb();

        var customerId = Guid.NewGuid();
        var staleLeadId = Guid.NewGuid();
        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = "site_demo",
            ExternalContactId = "447000000444",
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        });
        db.Leads.Add(new LeadRecord
        {
            Id = staleLeadId,
            SiteId = "site_demo",
            CustomerId = customerId,
            Status = LeadStatuses.Open,
            CreatedAtUtc = now.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, now, reuseWindowHours: 24);
        var site = BuildSite();

        var reply = await service.HandleMessageAsync(site, "447000000444", "Hello", "Alex", null, CancellationToken.None);

        Assert.That(reply.LeadId, Is.Null);
        Assert.That(reply.LeadJustCreated, Is.True);
    }

    [Test]
    public async Task reuses_project_fields_when_resuming_conversation_for_existing_open_lead()
    {
        var now = new DateTimeOffset(2026, 2, 9, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDb();

        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = "site_demo",
            ExternalContactId = "447000000555",
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        db.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            SiteId = "site_demo",
            CustomerId = customerId,
            Name = "Kitchen refresh",
            Status = "new",
            Summary = "Family kitchen refresh",
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["email"] = "jen@example.com"
            },
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        db.Leads.Add(new LeadRecord
        {
            Id = leadId,
            SiteId = "site_demo",
            CustomerId = customerId,
            ProjectId = projectId,
            Status = LeadStatuses.Open,
            CreatedAtUtc = now.AddHours(-2)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, now, reuseWindowHours: 72);
        var site = BuildSite();

        var reply = await service.HandleMessageAsync(site, "447000000555", "Hi", "Jen", null, CancellationToken.None);

        Assert.That(reply.LeadId, Is.EqualTo(leadId));
        Assert.That(reply.Collected.TryGetValue("email", out var email), Is.True);
        Assert.That(email, Is.EqualTo("jen@example.com"));
        Assert.That(reply.Collected.ContainsKey("project_summary"), Is.False);
        Assert.That(reply.Replies.Count, Is.EqualTo(1));
    }

    private static WhatsAppConversationService CreateService(LeadRelayDbContext db, DateTimeOffset now, int reuseWindowHours)
    {
        var clock = new FixedClock(now);
        var openAi = new OpenAIClient(new HttpClient(), Options.Create(new OpenAIOptions { Enabled = false }), NullLogger<OpenAIClient>.Instance);
        var openAiOptions = Options.Create(new OpenAIOptions { Enabled = false });
        var conversationOptions = Options.Create(new ConversationOptions
        {
            BotEnabled = true,
            UseLlm = false,
            SubmitLeadOnFirstMessage = true,
            SessionTimeoutHours = 1,
            ReuseOpenLeadWindowHours = reuseWindowHours
        });

        return new WhatsAppConversationService(clock, db, openAi, openAiOptions, conversationOptions, NullLogger<WhatsAppConversationService>.Instance);
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"wa-lead-selection-{Guid.NewGuid():N}")
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
            WhatsAppNumber = "447000000000",
            Fields = new[]
            {
                new ConversationField { Id = "email", Name = "Email", Description = "What is your email?" }
            }
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
