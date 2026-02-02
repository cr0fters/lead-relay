using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class WhatsAppWebhookController(
    ITokenService tokens,
    ISiteRepository sites,
    ILeadRepository leads,
    IEmailSender emailSender) : ControllerBase
{
    [HttpPost("/v1/webhooks/whatsapp")]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload, CancellationToken ct)
    {
        var text = ExtractText(payload);
        var waId = ExtractWaId(payload) ?? "unknown";

        var token = ExtractRefToken(text);
        if (token is null || !tokens.TryValidate(token, out var claims)) return Ok(new { ok = true });

        if (!claims.TryGetValue("siteId", out var siteId)) return Ok(new { ok = true });
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null) return Ok(new { ok = true });

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Intent = "website chat",
            Notes = $"waId={waId}; firstMessage={text}",
            PageUrl = claims.TryGetValue("pageUrl", out var p) ? p : null,
            Referrer = claims.TryGetValue("ref", out var r) ? r : null,
            Utm = claims.Where(kv => kv.Key.StartsWith("utm:", StringComparison.Ordinal)).ToDictionary(kv => kv.Key[4..], kv => kv.Value)
        };

        await leads.SaveAsync(lead, ct);

        var body = $"New lead for {site.Name}\n\nPage: {lead.PageUrl}\nReferrer: {lead.Referrer}\nNotes: {lead.Notes}\nUtm: {string.Join(", ", lead.Utm.Select(kv => kv.Key + "=" + kv.Value))}\n";
        await emailSender.SendAsync(site.OwnerEmail, $"New WhatsApp lead ({site.Name})", body, ct);

        return Ok(new { ok = true });
    }

    static string? ExtractText(JsonElement payload)
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
        catch { }
        return null;
    }

    static string? ExtractWaId(JsonElement payload)
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
        catch { }
        return null;
    }

    static string? ExtractRefToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var idx = text.IndexOf("ref=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + 4;
        var end = text.IndexOfAny([' ', '\n', '\r', '\t'], start);
        if (end < 0) end = text.Length;
        var token = text[start..end].Trim();
        return token.Length == 0 ? null : token;
    }
}
