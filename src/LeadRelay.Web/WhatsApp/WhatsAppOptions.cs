namespace LeadRelay.Web.WhatsApp;

public sealed record WhatsAppOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
    public string? VerifyToken { get; init; }
    public Dictionary<string, WhatsAppSenderOptions>? Senders { get; init; }
}

public sealed record WhatsAppSenderOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
}
