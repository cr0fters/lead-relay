using LeadRelay.Domain.Leads;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRecord
{
    public Guid Id { get; set; }
    public string SiteId { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? OwnerViewedAtUtc { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProjectId { get; set; }
    public string Channel { get; set; } = "api";
    public bool IsTest { get; set; }
    public string Status { get; set; } = "open";
    public bool IsBotPaused { get; set; }
    public Dictionary<string, string> Utm { get; set; } = new();
    public List<LeadConversationTurn> Conversation { get; set; } = new();
}
