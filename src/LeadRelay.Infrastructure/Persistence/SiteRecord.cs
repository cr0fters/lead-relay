using LeadRelay.Domain.Sites;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class SiteRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? BusinessSummary { get; set; }
    public List<string> AllowedDomains { get; set; } = new();
    public List<ConversationField> Fields { get; set; } = new();
    public List<ConversationField> OptionalFields { get; set; } = new();
    public string? IntroMessage { get; set; }
    public string OwnerEmail { get; set; } = "";
    public string WhatsAppNumber { get; set; } = "";
}
