using LeadRelay.Application.Abstractions;

namespace LeadRelay.Web.Messaging;

public sealed class EmailMessageChannel(IEmailSender emailSender) : IMessageChannel
{
    public string Name => "email";

    public async Task<MessageDispatchResult> SendTextAsync(string recipient, string text, CancellationToken ct)
    {
        var to = (recipient ?? "").Trim();
        if (string.IsNullOrWhiteSpace(to))
            return new MessageDispatchResult(false, "Email recipient is required.");

        if (string.IsNullOrWhiteSpace(text))
            return new MessageDispatchResult(false, "Email body is required.");

        await emailSender.SendAsync(to, "LeadRelay reply", text.Trim(), ct);
        return new MessageDispatchResult(true);
    }
}
