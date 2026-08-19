using LeadRelay.Web.Leads;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class WhatsAppWebhookController(
    WhatsAppSiteResolver siteResolver,
    LeadCaptureService leadCapture,
    WhatsAppClient whatsAppClient,
    WhatsAppConversationService conversations,
    IWhatsAppWebhookGuard webhookGuard,
    WhatsAppWebhookRateLimiter webhookRateLimiter,
    WhatsAppOnboardingService onboarding,
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
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var payloadBytes = await ReadBodyBytesAsync(ct);
        var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!webhookGuard.IsSignatureValid(payloadBytes, signatureHeader))
            return Unauthorized();

        using var payloadDocument = JsonDocument.Parse(payloadBytes);
        var payload = payloadDocument.RootElement.Clone();

        var text = ExtractText(payload);
        var waId = ExtractWaId(payload);
        var messageId = ExtractMessageId(payload);
        var contactName = ExtractContactName(payload);
        var phoneNumberId = ExtractPhoneNumberId(payload);
        var displayPhoneNumber = ExtractDisplayPhoneNumber(payload);
        if (string.IsNullOrWhiteSpace(waId)) return Ok(new { ok = true });
        if (string.IsNullOrWhiteSpace(messageId))
        {
            logger.LogWarning("Incoming WhatsApp message from {WaId} had no message ID and was not processed.", waId);
            return Ok(new { ok = true });
        }

        var site = await siteResolver.ResolveAsync(phoneNumberId, displayPhoneNumber, ct);
        if (site is null) return Ok(new { ok = true });
        if (!webhookRateLimiter.TryAcquire(site.Id, waId))
            return StatusCode(StatusCodes.Status429TooManyRequests);

        var receiptStarted = await webhookGuard.TryBeginProcessingAsync(site.Id, messageId, ct);
        if (!receiptStarted)
            return Ok(new { ok = true, duplicate = true });

        var sideEffectsStarted = false;
        try
        {
            var isTest = await onboarding.RecordInboundAsync(site.Id, waId, ct);
            var reply = await conversations.HandleMessageAsync(site, waId, text, contactName, null, ct);
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
                    NotifyOwner: false,
                    IsTest: isTest,
                    ProjectSummary: reply.ProjectSummary),
                ct);

            await webhookGuard.MarkSideEffectsStartedAsync(site.Id, messageId, ct);
            sideEffectsStarted = true;

            foreach (var message in reply.Replies)
                await whatsAppClient.SendTextAsync(waId, message, site.Id, ct);

            if (reply.LeadJustCreated && captured.Lead is not null)
                await leadCapture.NotifyOwnerAsync(site, captured.Lead, reply.Collected, ct);

            await webhookGuard.MarkProcessedAsync(site.Id, messageId, ct);
        }
        catch
        {
            if (!sideEffectsStarted)
            {
                try
                {
                    await webhookGuard.AbandonAsync(site.Id, messageId, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to release WhatsApp receipt {MessageId} for retry.", messageId);
                }
            }
            else
            {
                logger.LogError(
                    "WhatsApp processing failed after side effects started for message {MessageId}; the receipt was retained to prevent duplicate sends.",
                    messageId);
            }
            throw;
        }

        return Ok(new { ok = true });
    }

    private async Task<byte[]> ReadBodyBytesAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        Request.Body.Position = 0;
        return buffer.ToArray();
    }

    private string? ExtractText(JsonElement payload)
    {
        try
        {
            if (TryGetFirstMessage(payload, out var firstMessage) &&
                firstMessage.TryGetProperty("text", out var nestedText))
            {
                if (nestedText.ValueKind == JsonValueKind.String) return nestedText.GetString();
                if (nestedText.ValueKind == JsonValueKind.Object &&
                    nestedText.TryGetProperty("body", out var nestedBody) &&
                    nestedBody.ValueKind == JsonValueKind.String)
                {
                    return nestedBody.GetString();
                }
            }

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
            if (TryGetFirstMessage(payload, out var firstMessage) &&
                firstMessage.TryGetProperty("from", out var messageFrom) &&
                messageFrom.ValueKind == JsonValueKind.String)
            {
                return messageFrom.GetString();
            }

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

    private string? ExtractMessageId(JsonElement payload)
    {
        try
        {
            if (TryGetFirstMessage(payload, out var firstMessage) &&
                firstMessage.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }

            if (payload.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("id", out var nestedId) &&
                nestedId.ValueKind == JsonValueKind.String)
            {
                return nestedId.GetString();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to extract message id from WhatsApp payload");
        }

        return null;
    }

    private string? ExtractContactName(JsonElement payload)
    {
        try
        {
            if (TryGetValue(payload, out var value) &&
                value.TryGetProperty("contacts", out var nestedContacts) &&
                nestedContacts.ValueKind == JsonValueKind.Array &&
                nestedContacts.GetArrayLength() > 0)
            {
                var firstNested = nestedContacts[0];
                if (firstNested.TryGetProperty("profile", out var nestedProfile) &&
                    nestedProfile.ValueKind == JsonValueKind.Object &&
                    nestedProfile.TryGetProperty("name", out var nestedName) &&
                    nestedName.ValueKind == JsonValueKind.String)
                {
                    return nestedName.GetString();
                }
            }

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

    private string? ExtractPhoneNumberId(JsonElement payload)
    {
        try
        {
            if (TryGetMetadata(payload, out var metadata) &&
                metadata.TryGetProperty("phone_number_id", out var phoneNumberId) &&
                phoneNumberId.ValueKind == JsonValueKind.String)
            {
                return phoneNumberId.GetString();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to extract phone_number_id from WhatsApp payload");
        }

        return null;
    }

    private string? ExtractDisplayPhoneNumber(JsonElement payload)
    {
        try
        {
            if (TryGetMetadata(payload, out var metadata) &&
                metadata.TryGetProperty("display_phone_number", out var displayPhoneNumber) &&
                displayPhoneNumber.ValueKind == JsonValueKind.String)
            {
                return displayPhoneNumber.GetString();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to extract display phone number from WhatsApp payload");
        }

        return null;
    }

    private static bool TryGetMetadata(JsonElement payload, out JsonElement metadata)
    {
        metadata = default;
        if (!TryGetValue(payload, out var value)) return false;
        if (!value.TryGetProperty("metadata", out metadata)) return false;
        return metadata.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetFirstMessage(JsonElement payload, out JsonElement message)
    {
        message = default;
        if (!TryGetValue(payload, out var value)) return false;
        if (!value.TryGetProperty("messages", out var messages)) return false;
        if (messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() == 0) return false;

        message = messages[0];
        return message.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetValue(JsonElement payload, out JsonElement value)
    {
        value = default;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("entry", out var entries) &&
            entries.ValueKind == JsonValueKind.Array &&
            entries.GetArrayLength() > 0)
        {
            var entry = entries[0];
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("changes", out var changes) &&
                changes.ValueKind == JsonValueKind.Array &&
                changes.GetArrayLength() > 0)
            {
                var change = changes[0];
                if (change.ValueKind == JsonValueKind.Object &&
                    change.TryGetProperty("value", out var nestedValue) &&
                    nestedValue.ValueKind == JsonValueKind.Object)
                {
                    value = nestedValue;
                    return true;
                }
            }
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            value = payload;
            return true;
        }

        return false;
    }

}
