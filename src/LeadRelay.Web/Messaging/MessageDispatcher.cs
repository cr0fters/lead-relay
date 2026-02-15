using LeadRelay.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Messaging;

public sealed class MessageDispatcher(
    IEnumerable<IMessageChannel> channels,
    IOptions<MessagingOptions> options,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
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

        var maxRetries = Math.Max(0, options.Value.MaxRetries);
        var retryDelayMs = Math.Max(0, options.Value.RetryDelayMilliseconds);
        MessageDispatchResult? lastResult = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                lastResult = await sender.SendTextAsync(recipient, text, siteId, ct);
                if (lastResult.Sent)
                    return lastResult;
            }
            catch (Exception exception) when (attempt < maxRetries)
            {
                logger.LogWarning(exception, "Outbound dispatch attempt {Attempt} failed for channel {Channel}. Retrying.", attempt + 1, channel);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Outbound dispatch failed for channel {Channel}.", channel);
                return new MessageDispatchResult(false, "Failed to send message.");
            }

            if (attempt < maxRetries && retryDelayMs > 0)
                await Task.Delay(retryDelayMs, ct);
        }

        return lastResult ?? new MessageDispatchResult(false, "Failed to send message.");
    }
}
