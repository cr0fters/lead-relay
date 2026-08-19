namespace LeadRelay.Infrastructure.Persistence;

public sealed class WhatsAppConnectionRecord
{
    public string SiteId { get; set; } = "";
    public string WabaId { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string DisplayPhoneNumber { get; set; } = "";
    public string AccessTokenCiphertext { get; set; } = "";
    public string Status { get; set; } = "action_required";
    public DateTimeOffset? WebhookSubscribedAtUtc { get; set; }
    public DateTimeOffset? LastValidatedAtUtc { get; set; }
    public DateTimeOffset? LastInboundAtUtc { get; set; }
    public DateTimeOffset? LastOutboundTestAtUtc { get; set; }
    public string? LastOutboundTestRecipient { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
