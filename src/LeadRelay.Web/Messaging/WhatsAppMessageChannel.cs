using LeadRelay.Application.Abstractions;
using LeadRelay.Web.WhatsApp;

namespace LeadRelay.Web.Messaging;

public sealed class WhatsAppMessageChannel(WhatsAppClient client) : IMessageChannel
{
    public string Name => "whatsapp";

    public async Task<MessageDispatchResult> SendTextAsync(string recipient, string text, string? siteId, CancellationToken ct)
    {
        var sent = await client.SendTextAsync(recipient, text, siteId, ct);
        return sent
            ? new MessageDispatchResult(true)
            : new MessageDispatchResult(false, "WhatsApp send failed.");
    }
}
