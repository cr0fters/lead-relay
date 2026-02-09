using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Leads;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Web.Controllers;

public sealed class DebugController(
    ISiteRepository sites,
    LeadCaptureService leadCapture,
    ILeadRepository leads,
    LeadRelay.Infrastructure.Persistence.LeadRelayDbContext db,
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
        var captured = await leadCapture.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: waId,
                ContactName: contactName,
                FallbackMessage: message,
                Fields: reply.Collected,
                Conversation: reply.History
                    .Select(x => new LeadCaptureTurn(x.Role, x.Text, x.AtUtc))
                    .ToList(),
                LeadId: reply.LeadId,
                LeadCreatedAtUtc: reply.LeadCreatedAtUtc,
                NotifyOwner: reply.LeadJustCreated),
            ct);

        if (captured.Lead is not null)
            await conversations.BindLeadAsync(site.Id, waId, captured.Lead.Id, captured.Lead.CreatedAtUtc, ct);

        return Ok(new
        {
            reply = reply.ReplyText,
            replies = reply.Replies,
            completed = reply.IsComplete,
            collected = reply.Collected,
            history = reply.History,
            leadId = captured.Lead?.Id ?? reply.LeadId
        });
    }

    [HttpGet("/debug/whatsapp/leads")]
    public async Task<IActionResult> Leads([FromQuery] int limit, CancellationToken ct)
    {
        var items = await leads.GetRecentAsync(limit <= 0 ? 20 : limit, ct);
        return Ok(items.Select(x => new
        {
            id = x.Id,
            siteId = x.SiteId,
            name = x.Name,
            phone = x.Phone,
            email = x.Email,
            createdAtUtc = x.CreatedAtUtc
        }));
    }

    [HttpGet("/debug/whatsapp/leads/{id:guid}")]
    public async Task<IActionResult> LeadDetails(Guid id, CancellationToken ct)
    {
        var lead = await leads.GetByIdAsync(id, ct);
        if (lead is null) return NotFound();

        var contactName = await db.ConversationStates.AsNoTracking()
            .Where(x => x.SiteId == lead.SiteId && x.WaId == lead.Phone)
            .Select(x => x.ContactName)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            id = lead.Id,
            siteId = lead.SiteId,
            name = lead.Name,
            contactName,
            phone = lead.Phone,
            email = lead.Email,
            createdAtUtc = lead.CreatedAtUtc,
            fields = lead.Fields,
            conversation = lead.Conversation
        });
    }

    [HttpPost("/debug/whatsapp/pause")]
    public async Task<IActionResult> Pause([FromForm] string waId, [FromForm] bool paused, CancellationToken ct)
    {
        await conversations.SetPausedAsync("site_demo", waId, paused, ct);
        return Ok(new { ok = true, paused });
    }
}
