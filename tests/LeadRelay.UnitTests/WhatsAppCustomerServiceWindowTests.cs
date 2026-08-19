using LeadRelay.Domain.Leads;
using LeadRelay.Web.WhatsApp;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppCustomerServiceWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void latest_customer_message_opens_a_rolling_24_hour_window()
    {
        var conversation = new[]
        {
            new LeadConversationTurn("user", "Initial message", Now.AddHours(-30)),
            new LeadConversationTurn("owner", "Reply", Now.AddHours(-2)),
            new LeadConversationTurn("customer", "Latest inbound", Now.AddHours(-1))
        };

        var status = WhatsAppCustomerServiceWindow.Evaluate(conversation, Now);

        Assert.That(status.IsOpen, Is.True);
        Assert.That(status.LastInboundAtUtc, Is.EqualTo(Now.AddHours(-1)));
        Assert.That(status.ClosesAtUtc, Is.EqualTo(Now.AddHours(23)));
    }

    [TestCase(-1, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    public void window_closes_at_exactly_24_hours(int secondsPastBoundary, bool expectedOpen)
    {
        var inboundAt = Now.AddHours(-24).AddSeconds(-secondsPastBoundary);

        var status = WhatsAppCustomerServiceWindow.Evaluate(
            [new LeadConversationTurn("user", "Hello", inboundAt)],
            Now);

        Assert.That(status.IsOpen, Is.EqualTo(expectedOpen));
    }

    [Test]
    public void owner_and_automation_messages_do_not_open_the_window()
    {
        var status = WhatsAppCustomerServiceWindow.Evaluate(
            [
                new LeadConversationTurn("owner", "Owner reply", Now.AddMinutes(-5)),
                new LeadConversationTurn("assistant", "Automated reply", Now.AddMinutes(-1))
            ],
            Now);

        Assert.That(status.IsOpen, Is.False);
        Assert.That(status.LastInboundAtUtc, Is.Null);
        Assert.That(status.ClosesAtUtc, Is.Null);
    }
}
