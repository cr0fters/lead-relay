using LeadRelay.Domain.Leads;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRecord
{
    public Guid Id { get; set; }
    public string SiteId { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProjectId { get; set; }
    public string Channel { get; set; } = "api";
    public string Status { get; set; } = "open";
    public Dictionary<string, string> Utm { get; set; } = new();
    public List<LeadConversationTurn> Conversation { get; set; } = new();
}
