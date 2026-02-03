using LeadRelay.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task EnsureSeededAsync(LeadRelayDbContext db, CancellationToken ct)
    {
        if (await db.Sites.AsNoTracking().AnyAsync(x => x.Id == "site_demo", ct))
            return;

        db.Sites.Add(new SiteRecord
        {
            Id = "site_demo",
            Name = "Spaces by Kelly",
            BusinessSummary = "Interior design company specialising in modern, family-friendly spaces.",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            AllowedDomains = ["localhost"],
            IntroMessage =
                "Hey, thanks for reaching out to Spaces by Kelly! I'm here to get the ball rolling and gather a few details. Kelly will jump in shortly.",
            Fields =
            [
                new ConversationField
                {
                    Key = "project_description",
                    Prompt =
                        "Tell me a little about your project! What space are you designing? What's your biggest challenge? Any inspiration?"
                }
            ],
            OptionalFields =
            [
                new ConversationField
                {
                    Key = "timeline",
                    Prompt = "Do you have a rough timeline in mind?"
                },
                new ConversationField
                {
                    Key = "budget",
                    Prompt = "Do you have a rough budget range you're aiming for?"
                },
                new ConversationField
                {
                    Key = "location",
                    Prompt = "Where is the project located?"
                }
            ]
        });

        await db.SaveChangesAsync(ct);
    }
}
