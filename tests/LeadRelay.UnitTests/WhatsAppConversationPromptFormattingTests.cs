using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.AI;
using LeadRelay.Web.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppConversationPromptFormattingTests
{
    [Test]
    public async Task first_prompt_uses_sentence_case_field_name()
    {
        var now = new DateTimeOffset(2026, 2, 9, 22, 0, 0, TimeSpan.Zero);
        await using var db = CreateDb();
        var service = CreateService(db, now);

        var site = new Site
        {
            Id = "site_demo",
            Name = "Demo",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            Fields = new[]
            {
                new ConversationField
                {
                    Id = "project_overview",
                    Name = "Project overview",
                    Description = "What space is being designed and what is the main challenge?"
                }
            }
        };

        var reply = await service.HandleMessageAsync(site, "447000000111", "", "Jordan", null, CancellationToken.None);

        Assert.That(reply.Replies.Count, Is.EqualTo(2));
        Assert.That(reply.Replies[1], Is.EqualTo("Could you share your project overview? What space is being designed and what is the main challenge?"));
    }

    private static WhatsAppConversationService CreateService(LeadRelayDbContext db, DateTimeOffset now)
    {
        var clock = new FixedClock(now);
        var openAi = new OpenAIClient(new HttpClient(), Options.Create(new OpenAIOptions { Enabled = false }), NullLogger<OpenAIClient>.Instance);
        var openAiOptions = Options.Create(new OpenAIOptions { Enabled = false });
        var conversationOptions = Options.Create(new ConversationOptions
        {
            BotEnabled = true,
            UseLlm = false,
            SubmitLeadOnFirstMessage = false,
            SessionTimeoutHours = 1,
            ReuseOpenLeadWindowHours = 72
        });

        return new WhatsAppConversationService(clock, db, openAi, openAiOptions, conversationOptions, NullLogger<WhatsAppConversationService>.Instance);
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"wa-prompt-formatting-{Guid.NewGuid():N}")
            .Options;

        return new LeadRelayDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
