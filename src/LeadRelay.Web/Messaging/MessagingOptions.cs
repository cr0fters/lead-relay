namespace LeadRelay.Web.Messaging;

public sealed record MessagingOptions
{
    public int MaxRetries { get; init; } = 2;
    public int RetryDelayMilliseconds { get; init; } = 100;
}
