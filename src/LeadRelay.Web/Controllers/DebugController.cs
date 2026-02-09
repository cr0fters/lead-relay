using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Leads;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class DebugController(
    ISiteRepository sites,
    LeadCaptureService leadCapture,
    ILeadRepository leads,
    WhatsAppConversationService conversations) : Controller
{
    [HttpGet("/debug/whatsapp")]
    public ViewResult WhatsApp()
    {
        return View();
    }

    [HttpPost("/debug/whatsapp/send")]
    public async Task<IActionResult> Send([FromForm] string contactId, [FromForm] string message, [FromForm] string? contactName, [FromForm] string? systemPrompt, CancellationToken ct)
    {
        var site = await ResolveDefaultSiteAsync(ct);
        if (site is null) return NotFound();

        var reply = await conversations.HandleMessageAsync(site, contactId, message, contactName, systemPrompt, ct);
        var captured = await leadCapture.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: "whatsapp",
                ExternalContactId: contactId,
                ContactName: contactName,
                FallbackMessage: message,
                Fields: reply.Collected,
                Conversation: reply.History
                    .Select(x => new LeadCaptureTurn(x.Role, x.Text, x.AtUtc))
                    .ToList(),
                LeadId: reply.LeadId,
                LeadCreatedAtUtc: reply.LeadCreatedAtUtc,
                NotifyOwner: reply.LeadJustCreated,
                ProjectSummary: reply.ProjectSummary),
            ct);

        return Ok(new
        {
            reply = reply.ReplyText,
            replies = reply.Replies,
            completed = reply.IsComplete,
            collected = reply.Collected,
            projectSummary = reply.ProjectSummary,
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

        return Ok(new
        {
            id = lead.Id,
            siteId = lead.SiteId,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            createdAtUtc = lead.CreatedAtUtc,
            projectSummary = lead.ProjectSummary,
            fields = lead.Fields,
            conversation = lead.Conversation
        });
    }

    [HttpPost("/debug/whatsapp/pause")]
    public async Task<IActionResult> Pause([FromForm] Guid leadId, [FromForm] bool paused, CancellationToken ct)
    {
        var lead = await leads.GetByIdAsync(leadId, ct);
        if (lead is null) return NotFound();
        lead.IsBotPaused = paused;
        await leads.SaveAsync(lead, ct);
        return Ok(new { ok = true, paused });
    }

    private async Task<LeadRelay.Domain.Sites.Site?> ResolveDefaultSiteAsync(CancellationToken ct)
    {
        var allSites = await sites.GetAllAsync(ct);
        return allSites.FirstOrDefault();
    }
}
