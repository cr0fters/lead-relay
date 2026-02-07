namespace LeadRelay.Application.Abstractions;

public interface IMessageDispatcher
{
    Task<MessageDispatchResult> SendTextAsync(string channel, string recipient, string text, CancellationToken ct);
}
