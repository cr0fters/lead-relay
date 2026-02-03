using LeadRelay.Domain.Leads;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRecord
{
    public Guid Id { get; set; }
    public string SiteId { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Intent { get; set; }
    public string? Notes { get; set; }
    public string? PageUrl { get; set; }
    public string? Referrer { get; set; }
    public Dictionary<string, string> Utm { get; set; } = new();
    public Dictionary<string, string> Fields { get; set; } = new();
    public List<LeadConversationTurn> Conversation { get; set; } = new();
}
