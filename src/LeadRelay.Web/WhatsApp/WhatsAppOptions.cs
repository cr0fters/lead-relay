namespace LeadRelay.Web.WhatsApp;

public sealed record WhatsAppOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
    public string? VerifyToken { get; init; }
    public string? AppSecret { get; init; }
    public bool RequireSignatureValidation { get; init; } = true;
    public string? CredentialEncryptionKey { get; init; }
    public string GraphApiBaseUrl { get; init; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; init; } = "v23.0";
    public int IdempotencyProcessingLeaseMinutes { get; init; } = 30;
    public int ProcessedReceiptRetentionDays { get; init; } = 30;
    public Dictionary<string, WhatsAppSenderOptions>? Senders { get; init; }
}

public sealed record WhatsAppSenderOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
}
