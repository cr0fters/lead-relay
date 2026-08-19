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
    public bool EmbeddedSignupEnabled { get; init; }
    public string? MetaAppId { get; init; }
    public string? EmbeddedSignupConfigurationId { get; init; }
    public string EmbeddedSignupVersion { get; init; } = "v4";
    public int IdempotencyProcessingLeaseMinutes { get; init; } = 30;
    public int ProcessedReceiptRetentionDays { get; init; } = 30;
    public Dictionary<string, WhatsAppSenderOptions>? Senders { get; init; }

    public bool IsEmbeddedSignupConfigured =>
        EmbeddedSignupEnabled &&
        IsMetaIdentifier(MetaAppId) &&
        IsMetaIdentifier(EmbeddedSignupConfigurationId) &&
        !string.IsNullOrWhiteSpace(AppSecret) &&
        string.Equals(EmbeddedSignupVersion?.Trim(), "v4", StringComparison.Ordinal);

    private static bool IsMetaIdentifier(string? value)
    {
        var candidate = value?.Trim();
        return !string.IsNullOrEmpty(candidate) && candidate.Length <= 64 && candidate.All(char.IsDigit);
    }
}

public sealed record WhatsAppSenderOptions
{
    public string? AccessToken { get; init; }
    public string? MessagesEndpoint { get; init; }
}
