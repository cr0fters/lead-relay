using LeadRelay.Application.Abstractions;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class DebugController(
    ISiteRepository sites,
    WhatsAppConversationService conversations) : Controller
{
    [HttpGet("/debug/whatsapp")]
    public ViewResult WhatsApp()
    {
        return View();
    }

    [HttpPost("/debug/whatsapp/send")]
    public async Task<IActionResult> Send([FromForm] string waId, [FromForm] string message, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync("site_demo", ct);
        if (site is null) return NotFound();

        var reply = conversations.HandleMessage(site, waId, message);
        return Ok(new
        {
            reply = reply.ReplyText,
            completed = reply.IsComplete,
            collected = reply.Collected
        });
    }
}
