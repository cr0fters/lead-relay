using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Leads;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class WhatsAppWebhookController(
    ISiteRepository sites,
    LeadCaptureService leadCapture,
    WhatsAppClient whatsAppClient,
    WhatsAppConversationService conversations,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    [HttpGet("/v1/webhooks/whatsapp")]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = options.Value.VerifyToken;
        if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(verifyToken, expected, StringComparison.Ordinal))
        {
            return Content(challenge ?? "", "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("/v1/webhooks/whatsapp")]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload, CancellationToken ct)
    {
        var text = ExtractText(payload);
        var waId = ExtractWaId(payload);
        var contactName = ExtractContactName(payload);
        if (string.IsNullOrWhiteSpace(waId)) return Ok(new { ok = true });

        // POC: no attribution, assume a single configured site.
        var site = await ResolveDefaultSiteAsync(ct);
        if (site is null) return Ok(new { ok = true });

        var reply = await conversations.HandleMessageAsync(site, waId, text, contactName, null, ct);
        foreach (var message in reply.Replies)
            await whatsAppClient.SendTextAsync(waId, message, ct);

        var captured = await leadCapture.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: waId,
                ContactName: contactName,
                FallbackMessage: text,
                Fields: reply.Collected,
                Conversation: reply.History
                    .Select(x => new LeadCaptureTurn(x.Role, x.Text, x.AtUtc))
                    .ToList(),
                LeadId: reply.LeadId,
                LeadCreatedAtUtc: reply.LeadCreatedAtUtc,
                NotifyOwner: reply.LeadJustCreated,
                ProjectSummary: reply.ProjectSummary),
            ct);

        return Ok(new { ok = true });
    }

    private string? ExtractText(JsonElement payload)
    {
        try
        {
            if (payload.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) return t.GetString();
            if (payload.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) return m.GetString();
            if (payload.TryGetProperty("messages", out var ms) && ms.ValueKind == JsonValueKind.Array && ms.GetArrayLength() > 0)
            {
                var first = ms[0];
                if (first.TryGetProperty("text", out var txt))
                {
                    if (txt.ValueKind == JsonValueKind.String) return txt.GetString();
                    if (txt.ValueKind == JsonValueKind.Object && txt.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String) return body.GetString();
                }
            }
        }
        catch(Exception exception)
        {
            logger.LogError(exception, "Failed to extract text from WhatsApp message");
        }

        return null;
    }

    private string? ExtractWaId(JsonElement payload)
    {
        try
        {
            if (payload.TryGetProperty("waId", out var id) && id.ValueKind == JsonValueKind.String) return id.GetString();
            if (payload.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.String) return from.GetString();
            if (payload.TryGetProperty("messages", out var ms) && ms.ValueKind == JsonValueKind.Array && ms.GetArrayLength() > 0)
            {
                var first = ms[0];
                if (first.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.String) return f.GetString();
            }
        }
        catch(Exception exception)
        {
            logger.LogError(exception, "Failed to extract waId from WhatsApp message");
        }

        return null;
    }

    private string? ExtractContactName(JsonElement payload)
    {
        try
        {
            if (payload.TryGetProperty("contacts", out var contacts) &&
                contacts.ValueKind == JsonValueKind.Array &&
                contacts.GetArrayLength() > 0)
            {
                var first = contacts[0];
                if (first.TryGetProperty("profile", out var profile) &&
                    profile.ValueKind == JsonValueKind.Object &&
                    profile.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    return name.GetString();
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to extract contact name from WhatsApp payload");
        }

        return null;
    }

    private async Task<LeadRelay.Domain.Sites.Site?> ResolveDefaultSiteAsync(CancellationToken ct)
    {
        var allSites = await sites.GetAllAsync(ct);
        return allSites.FirstOrDefault();
    }
}
