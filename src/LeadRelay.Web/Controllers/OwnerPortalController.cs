using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerPortalController(ILeadRepository leads, WhatsAppClient whatsApp) : Controller
{
    [HttpGet("/owner")]
    public async Task<IActionResult> Index([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var items = await leads.GetRecentBySiteAsync(auth.SiteId, limit <= 0 ? 50 : limit, ct);
        var model = new OwnerDashboardModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Leads = items.Select(x => new OwnerLeadListItem
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                Email = x.Email,
                CreatedAtUtc = x.CreatedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpGet("/owner/leads/{id:guid}")]
    public async Task<IActionResult> Lead([FromRoute] Guid id, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        return View(ToDetailModel(auth, lead));
    }

    [HttpPost("/owner/leads/{id:guid}/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply([FromRoute] Guid id, [FromForm] string? message, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var model = ToDetailModel(auth, lead);
        var text = message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            model.Error = "Message is required.";
            return View("Lead", model);
        }

        if (string.IsNullOrWhiteSpace(lead.Phone))
        {
            model.Error = "This lead has no phone number to send a WhatsApp message.";
            return View("Lead", model);
        }

        var sent = await whatsApp.SendTextAsync(lead.Phone, text, ct);
        if (!sent)
        {
            model.Error = "Failed to send WhatsApp reply.";
            return View("Lead", model);
        }

        model.Success = "Reply sent.";
        return View("Lead", model);
    }

    private OwnerAuthContext? GetAuthContext()
    {
        return HttpContext.Items.TryGetValue(OwnerAuthMiddleware.ContextKey, out var value)
            ? value as OwnerAuthContext
            : null;
    }

    private static OwnerLeadDetailModel ToDetailModel(OwnerAuthContext auth, Lead lead)
    {
        return new OwnerLeadDetailModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Id = lead.Id,
            Name = lead.Name,
            Email = lead.Email,
            Phone = lead.Phone,
            Notes = lead.Notes,
            Intent = lead.Intent,
            CreatedAtUtc = lead.CreatedAtUtc,
            Fields = lead.Fields,
            Conversation = lead.Conversation
        };
    }

    public sealed class OwnerDashboardModel
    {
        public string SiteId { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public List<OwnerLeadListItem> Leads { get; set; } = new();
    }

    public sealed class OwnerLeadListItem
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }

    public sealed class OwnerLeadDetailModel
    {
        public string SiteId { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Notes { get; set; }
        public string? Intent { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<LeadConversationTurn> Conversation { get; set; } = Array.Empty<LeadConversationTurn>();
        public string? Error { get; set; }
        public string? Success { get; set; }
    }
}
