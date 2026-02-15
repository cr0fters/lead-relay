using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class MessageDispatcherTests
{
    [Test]
    public async Task dispatches_to_matching_channel()
    {
        var dispatcher = CreateDispatcher(new FakeChannel("whatsapp", true));

        var result = await dispatcher.SendTextAsync("whatsapp", "447000000000", "hello", "site_a", CancellationToken.None);

        Assert.That(result.Sent, Is.True);
    }

    [Test]
    public async Task returns_error_for_unknown_channel()
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.SendTextAsync("sms", "123", "hello", "site_a", CancellationToken.None);

        Assert.That(result.Sent, Is.False);
        Assert.That(result.Error, Does.Contain("Unsupported channel"));
    }

    [Test]
    public async Task retries_until_send_succeeds()
    {
        var channel = new RetryChannel("whatsapp", failuresBeforeSuccess: 2);
        var dispatcher = CreateDispatcher(channel, maxRetries: 3, retryDelayMilliseconds: 0);

        var result = await dispatcher.SendTextAsync("whatsapp", "447000000000", "hello", "site_a", CancellationToken.None);

        Assert.That(result.Sent, Is.True);
        Assert.That(channel.AttemptCount, Is.EqualTo(3));
    }

    [Test]
    public async Task returns_failed_result_when_retries_exhausted()
    {
        var channel = new RetryChannel("whatsapp", failuresBeforeSuccess: 10);
        var dispatcher = CreateDispatcher(channel, maxRetries: 2, retryDelayMilliseconds: 0);

        var result = await dispatcher.SendTextAsync("whatsapp", "447000000000", "hello", "site_a", CancellationToken.None);

        Assert.That(result.Sent, Is.False);
        Assert.That(channel.AttemptCount, Is.EqualTo(3));
    }

    [Test]
    public async Task retries_when_channel_throws_then_succeeds()
    {
        var channel = new ThrowThenSucceedChannel("whatsapp");
        var dispatcher = CreateDispatcher(channel, maxRetries: 1, retryDelayMilliseconds: 0);

        var result = await dispatcher.SendTextAsync("whatsapp", "447000000000", "hello", "site_a", CancellationToken.None);

        Assert.That(result.Sent, Is.True);
        Assert.That(channel.AttemptCount, Is.EqualTo(2));
    }

    private static MessageDispatcher CreateDispatcher(
        IMessageChannel? channel = null,
        int maxRetries = 2,
        int retryDelayMilliseconds = 0)
    {
        var channels = channel is null ? Array.Empty<IMessageChannel>() : [channel];
        return new MessageDispatcher(
            channels,
            Options.Create(new MessagingOptions
            {
                MaxRetries = maxRetries,
                RetryDelayMilliseconds = retryDelayMilliseconds
            }),
            NullLogger<MessageDispatcher>.Instance);
    }

    private sealed class FakeChannel(string name, bool sent) : IMessageChannel
    {
        public string Name { get; } = name;

        public Task<MessageDispatchResult> SendTextAsync(string recipient, string text, string? siteId, CancellationToken ct)
            => Task.FromResult(new MessageDispatchResult(sent));
    }

    private sealed class RetryChannel(string name, int failuresBeforeSuccess) : IMessageChannel
    {
        public string Name { get; } = name;
        public int AttemptCount { get; private set; }

        public Task<MessageDispatchResult> SendTextAsync(string recipient, string text, string? siteId, CancellationToken ct)
        {
            AttemptCount++;
            return Task.FromResult(AttemptCount <= failuresBeforeSuccess
                ? new MessageDispatchResult(false, "Temporary failure")
                : new MessageDispatchResult(true));
        }
    }

    private sealed class ThrowThenSucceedChannel(string name) : IMessageChannel
    {
        public string Name { get; } = name;
        public int AttemptCount { get; private set; }

        public Task<MessageDispatchResult> SendTextAsync(string recipient, string text, string? siteId, CancellationToken ct)
        {
            AttemptCount++;
            if (AttemptCount == 1)
                throw new InvalidOperationException("Transient transport error");
            return Task.FromResult(new MessageDispatchResult(true));
        }
    }
}
