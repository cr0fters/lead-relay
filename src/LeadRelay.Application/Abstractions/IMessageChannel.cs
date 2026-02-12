namespace LeadRelay.Application.Abstractions;

public interface IMessageChannel
{
    string Name { get; }
    Task<MessageDispatchResult> SendTextAsync(string recipient, string text, string? siteId, CancellationToken ct);
}
