using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class InMemorySiteRepository : ISiteRepository
{
    private static readonly Site[] Sites =
    [
        new()
        {
            Id = "site_demo",
            Name = "Demo site",
            BusinessSummary = "Interior design company specialising in modern, family-friendly spaces.",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            AllowedDomains = ["localhost"],
            Fields =
            [
                new()
                {
                    Key = "name",
                    Prompt = "What's your name?"
                },
                new()
                {
                    Key = "email",
                    Prompt = "What's your email address?",
                    Type = ConversationFieldType.Email
                },
                new()
                {
                    Key = "project_description",
                    Prompt = "Can you describe the project?"
                }
            ]
        }
    ];

    public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct) => Task.FromResult(Sites.FirstOrDefault(x => x.Id == siteId));
}
