using System.Net;
using System.Text;
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

public sealed class WhatsAppLlmFieldInferenceTests
{
    [Test]
    public async Task does_not_infer_timeline_when_previous_question_was_not_timeline()
    {
        var now = new DateTimeOffset(2026, 2, 9, 22, 5, 0, TimeSpan.Zero);
        await using var db = CreateDb();
        var site = BuildSite();
        var leadId = await SeedOpenLeadAsync(
            db,
            now,
            "447000000666",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["project_overview"] = "bathroom"
            },
            new[]
            {
                new LeadConversationTurn("assistant", "What would you say is the main challenge you're facing with this project?", now.AddMinutes(-1))
            });

        var service = CreateService(
            db,
            now,
            """{"reply_text":"What’s your rough timeline for this project?","collected":[],"done":false,"project_summary":"Morgan is looking to design a bathroom."}""");

        var reply = await service.HandleMessageAsync(
            site,
            "447000000666",
            "I'd like to make it bigger, and have separate shower and bath",
            "Morgan",
            null,
            CancellationToken.None);

        Assert.That(reply.LeadId, Is.EqualTo(leadId));
        Assert.That(reply.Collected.ContainsKey("timeline"), Is.False);
    }

    [Test]
    public async Task infers_timeline_when_previous_question_is_for_timeline()
    {
        var now = new DateTimeOffset(2026, 2, 9, 22, 6, 0, TimeSpan.Zero);
        await using var db = CreateDb();
        var site = BuildSite();
        var leadId = await SeedOpenLeadAsync(
            db,
            now,
            "447000000777",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["project_overview"] = "bathroom"
            },
            new[]
            {
                new LeadConversationTurn("assistant", "What’s your rough timeline for this project?", now.AddMinutes(-1))
            });

        var service = CreateService(
            db,
            now,
            """{"reply_text":"Thanks for sharing. Could you share your budget range?","collected":[],"done":false,"project_summary":"Bathroom redesign project."}""");

        var reply = await service.HandleMessageAsync(
            site,
            "447000000777",
            "asap",
            "Morgan",
            null,
            CancellationToken.None);

        Assert.That(reply.LeadId, Is.EqualTo(leadId));
        Assert.That(reply.Collected.TryGetValue("timeline", out var timeline), Is.True);
        Assert.That(timeline, Is.EqualTo("asap"));
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
                new ConversationField { Id = "project_overview", Name = "Project overview", Description = "What space is being designed and what is the main challenge?" },
                new ConversationField { Id = "timeline", Name = "Timeline", Description = "Do you have a rough timeline in mind?" },
                new ConversationField { Id = "budget", Name = "Budget", Description = "Do you have a rough budget range you're aiming for?" }
            }
        };
    }

    private static async Task<Guid> SeedOpenLeadAsync(
        LeadRelayDbContext db,
        DateTimeOffset now,
        string contactId,
        Dictionary<string, string> projectFields,
        IReadOnlyList<LeadConversationTurn> conversation)
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        db.Customers.Add(new CustomerRecord
        {
            Id = customerId,
            SiteId = "site_demo",
            ExternalContactId = contactId,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        db.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            SiteId = "site_demo",
            CustomerId = customerId,
            Name = "Bathroom redesign",
            Status = "new",
            Summary = "Bathroom redesign project.",
            Fields = projectFields,
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
            CreatedAtUtc = now.AddHours(-2),
            Conversation = conversation.Select(x => new LeadConversationTurn(x.Role, x.Text, x.AtUtc)).ToList()
        });

        await db.SaveChangesAsync();
        return leadId;
    }

    private static WhatsAppConversationService CreateService(
        LeadRelayDbContext db,
        DateTimeOffset now,
        string llmOutputJson)
    {
        var clock = new FixedClock(now);
        var http = new HttpClient(new StubHttpMessageHandler(llmOutputJson));
        var openAiOptions = Options.Create(new OpenAIOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            Model = "gpt-test",
            BaseUrl = "https://example.test"
        });
        var openAi = new OpenAIClient(http, openAiOptions, NullLogger<OpenAIClient>.Instance);
        var conversationOptions = Options.Create(new ConversationOptions
        {
            BotEnabled = true,
            UseLlm = true,
            SubmitLeadOnFirstMessage = true,
            SessionTimeoutHours = 1,
            ReuseOpenLeadWindowHours = 72
        });

        return new WhatsAppConversationService(clock, db, openAi, openAiOptions, conversationOptions, NullLogger<WhatsAppConversationService>.Instance);
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"wa-llm-field-inference-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class StubHttpMessageHandler(string llmOutputJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = $$"""
                         {
                           "output_text": {{System.Text.Json.JsonSerializer.Serialize(llmOutputJson)}}
                         }
                         """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
