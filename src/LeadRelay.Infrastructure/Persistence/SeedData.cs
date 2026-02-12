using LeadRelay.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Infrastructure.Persistence;

public static class SeedData
{
    private const string DefaultSeedSiteId = "2c7f9e0e-487f-4adf-8f0c-68c0f0d7b204";

    public static async Task EnsureSeededAsync(LeadRelayDbContext db, CancellationToken ct)
    {
        if (await db.Sites.AsNoTracking().AnyAsync(ct))
            return;

        db.Sites.Add(new SiteRecord
        {
            Id = DefaultSeedSiteId,
            Name = "Spaces by Kelly",
            BusinessSummary = "Interior design company specialising in modern, family-friendly spaces.",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            WhatsAppPhoneNumberId = "demo-phone-number-id",
            AllowedDomains = ["localhost"],
            IntroMessage =
                "Hey, thanks for reaching out to Spaces by Kelly! I'm here to get the ball rolling and gather a few details. Kelly will jump in shortly.",
            Fields =
            [
                new ConversationField
                {
                    Id = "project_overview",
                    Name = "Project overview",
                    Description = "What space is being designed and what is the main challenge?"
                },
                new ConversationField
                {
                    Id = "timeline",
                    Name = "Timeline",
                    Description = "Do you have a rough timeline in mind?"
                },
                new ConversationField
                {
                    Id = "budget",
                    Name = "Budget",
                    Description = "Do you have a rough budget range you're aiming for?"
                },
                new ConversationField
                {
                    Id = "location",
                    Name = "Location",
                    Description = "Where is the project located?"
                }
            ]
        });

        await db.SaveChangesAsync(ct);
    }
}
