namespace LeadRelay.Web.Leads;

public sealed record LeadCaptureInput(
    string Channel,
    string? ExternalContactId,
    string? ContactName,
    string? FallbackMessage,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<LeadCaptureTurn> Conversation,
    Guid? LeadId = null,
    DateTimeOffset? LeadCreatedAtUtc = null,
    bool NotifyOwner = false,
    string? ExplicitName = null,
    string? ExplicitEmail = null,
    string? ExplicitPhone = null);
