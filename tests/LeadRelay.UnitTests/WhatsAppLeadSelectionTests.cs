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
                new ConversationField { Key = "email", Prompt = "What is your email?", Required = true, Type = ConversationFieldType.Email }
            }
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
