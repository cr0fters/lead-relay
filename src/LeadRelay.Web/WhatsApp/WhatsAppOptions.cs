namespace LeadRelay.Web.WhatsApp;

public sealed record WhatsAppOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
    public string? VerifyToken { get; init; }
    public string? AppSecret { get; init; }
    public bool RequireSignatureValidation { get; init; }
    public int IdempotencyTtlMinutes { get; init; } = 30;
    public Dictionary<string, WhatsAppSenderOptions>? Senders { get; init; }
}

public sealed record WhatsAppSenderOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
}
