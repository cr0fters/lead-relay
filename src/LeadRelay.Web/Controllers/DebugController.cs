using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Leads;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class DebugController(
    ISiteRepository sites,
    LeadCaptureService leadCapture,
    WhatsAppConversationService conversations) : Controller
{
    [HttpGet("/debug/whatsapp")]
    public ViewResult WhatsApp()
    {
        return View();
    }

    [HttpPost("/debug/whatsapp/send")]
    public async Task<IActionResult> Send([FromForm] string waId, [FromForm] string message, [FromForm] string? contactName, [FromForm] string? systemPrompt, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync("site_demo", ct);
        if (site is null) return NotFound();

        var reply = await conversations.HandleMessageAsync(site, waId, message, contactName, systemPrompt, ct);
        await leadCapture.CaptureAsync(
            site,
            waId,
            "website chat (debug)",
            message,
            reply,
            null,
            ct);

        return Ok(new
        {
            reply = reply.ReplyText,
            replies = reply.Replies,
            completed = reply.IsComplete,
            collected = reply.Collected,
            history = reply.History,
            leadId = reply.LeadId
        });
    }

    [HttpPost("/debug/whatsapp/pause")]
    public async Task<IActionResult> Pause([FromForm] string waId, [FromForm] bool paused, CancellationToken ct)
    {
        await conversations.SetPausedAsync("site_demo", waId, paused, ct);
        return Ok(new { ok = true, paused });
    }
}
