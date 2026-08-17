namespace LeadRelay.Web.WhatsApp;

public static class WhatsAppConnectionStatuses
{
    public const string ActionRequired = "action_required";
    public const string Connected = "connected";
}

public sealed record WhatsAppConnectRequest(
    string? WabaId,
    string? PhoneNumberId,
    string? DisplayPhoneNumber,
    string? AccessToken);

public sealed record WhatsAppConnectionResult(bool Succeeded, string? Error = null);

public sealed record WhatsAppConnectionSummary(
    bool Exists,
    string Status,
    string? WabaId,
    string? PhoneNumberId,
    string? DisplayPhoneNumber,
    DateTimeOffset? WebhookSubscribedAtUtc,
    DateTimeOffset? LastValidatedAtUtc,
    DateTimeOffset? LastInboundAtUtc,
    DateTimeOffset? LastOutboundTestAtUtc,
    string? LastError)
{
    public bool IsConnected => Exists && string.Equals(Status, WhatsAppConnectionStatuses.Connected, StringComparison.Ordinal);
    public bool IsWebhookSubscribed => WebhookSubscribedAtUtc.HasValue;
    public bool IsWebhookVerified => LastInboundAtUtc.HasValue;
    public bool HasSuccessfulTest => LastOutboundTestAtUtc.HasValue;
}
