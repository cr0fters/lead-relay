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
            Name = "Spaces by Kelly",
            BusinessSummary = "Interior design company specialising in modern, family-friendly spaces.",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            AllowedDomains = ["localhost"],
            IntroMessage = "Hey, thanks for reaching out to Spaces by Kelly! I'm here to get the ball rolling and gather a few details. Kelly will jump in shortly.",
            Fields =
            [
                new()
                {
                    Key = "project_description",
                    Prompt = "Tell me a little about your project! What space are you designing? What's your biggest challenge? Any inspiration?"
                }
            ],
            OptionalFields =
            [
                new()
                {
                    Key = "timeline",
                    Prompt = "Do you have a rough timeline in mind?"
                },
                new()
                {
                    Key = "budget",
                    Prompt = "Do you have a rough budget range you're aiming for?"
                },
                new()
                {
                    Key = "location",
                    Prompt = "Where is the project located?"
                }
            ]
        }
    ];

    public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct) => Task.FromResult(Sites.FirstOrDefault(x => x.Id == siteId));
}
