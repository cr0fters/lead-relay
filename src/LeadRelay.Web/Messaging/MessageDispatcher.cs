using LeadRelay.Application.Abstractions;

namespace LeadRelay.Web.Messaging;

public sealed class MessageDispatcher(IEnumerable<IMessageChannel> channels) : IMessageDispatcher
{
    private readonly Dictionary<string, IMessageChannel> _channels = channels
        .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public async Task<MessageDispatchResult> SendTextAsync(string channel, string recipient, string text, string? siteId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channel))
            return new MessageDispatchResult(false, "Channel is required.");

        if (!_channels.TryGetValue(channel.Trim(), out var sender))
            return new MessageDispatchResult(false, $"Unsupported channel '{channel}'.");

        return await sender.SendTextAsync(recipient, text, siteId, ct);
    }
}
