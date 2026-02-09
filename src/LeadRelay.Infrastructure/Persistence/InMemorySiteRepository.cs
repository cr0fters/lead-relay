using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class InMemorySiteRepository : ISiteRepository
{
    public const string DefaultSiteId = "2c7f9e0e-487f-4adf-8f0c-68c0f0d7b204";

    private static readonly List<Site> Sites =
    [
        new()
        {
            Id = DefaultSiteId,
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
                    Id = "project_overview",
                    Name = "Project overview",
                    Description = "What space is being designed and what is the main challenge?"
                }
                ,
                new()
                {
                    Id = "timeline",
                    Name = "Timeline",
                    Description = "When would you like to start or complete this project?"
                },
                new()
                {
                    Id = "budget",
                    Name = "Budget",
                    Description = "What budget range are you aiming for?"
                },
                new()
                {
                    Id = "location",
                    Name = "Location",
                    Description = "Where is the project located?"
                }
            ]
        }
    ];

    public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct) => Task.FromResult(Sites.FirstOrDefault(x => x.Id == siteId));

    public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<Site>>(Sites
            .OrderBy(x => x.Name)
            .ToList());
    }

    public Task UpsertAsync(Site site, CancellationToken ct)
    {
        var index = Sites.FindIndex(x => x.Id == site.Id);
        if (index >= 0)
        {
            Sites[index] = site;
        }
        else
        {
            Sites.Add(site);
        }

        return Task.CompletedTask;
    }
}
