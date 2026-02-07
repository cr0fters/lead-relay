using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Messaging;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class MessageDispatcherTests
{
    [Test]
    public async Task dispatches_to_matching_channel()
    {
        var dispatcher = new MessageDispatcher(new IMessageChannel[] { new FakeChannel("whatsapp", true) });

        var result = await dispatcher.SendTextAsync("whatsapp", "447000000000", "hello", CancellationToken.None);

        Assert.That(result.Sent, Is.True);
    }

    [Test]
    public async Task returns_error_for_unknown_channel()
    {
        var dispatcher = new MessageDispatcher(Array.Empty<IMessageChannel>());

        var result = await dispatcher.SendTextAsync("sms", "123", "hello", CancellationToken.None);

        Assert.That(result.Sent, Is.False);
        Assert.That(result.Error, Does.Contain("Unsupported channel"));
    }

    private sealed class FakeChannel(string name, bool sent) : IMessageChannel
    {
        public string Name { get; } = name;

        public Task<MessageDispatchResult> SendTextAsync(string recipient, string text, CancellationToken ct)
            => Task.FromResult(new MessageDispatchResult(sent));
    }
}
