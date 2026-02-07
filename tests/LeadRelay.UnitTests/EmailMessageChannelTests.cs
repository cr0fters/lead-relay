using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Messaging;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class EmailMessageChannelTests
{
    [Test]
    public async Task sends_email_when_recipient_and_text_present()
    {
        var sender = new RecordingEmailSender();
        var channel = new EmailMessageChannel(sender);

        var result = await channel.SendTextAsync("lead@example.com", "Hello there", CancellationToken.None);

        Assert.That(result.Sent, Is.True);
        Assert.That(sender.Sent.Count, Is.EqualTo(1));
        Assert.That(sender.Sent[0].To, Is.EqualTo("lead@example.com"));
        Assert.That(sender.Sent[0].Subject, Is.EqualTo("LeadRelay reply"));
    }

    [Test]
    public async Task returns_error_when_recipient_missing()
    {
        var channel = new EmailMessageChannel(new RecordingEmailSender());

        var result = await channel.SendTextAsync("", "Hello", CancellationToken.None);

        Assert.That(result.Sent, Is.False);
        Assert.That(result.Error, Is.EqualTo("Email recipient is required."));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, bodyText));
            return Task.CompletedTask;
        }
    }
}
