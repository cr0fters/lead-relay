namespace LeadRelay.Infrastructure.Persistence;

public sealed class WhatsAppMessageReceiptRecord
{
    public string SiteId { get; set; } = "";
    public string MessageId { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? SideEffectsStartedAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
