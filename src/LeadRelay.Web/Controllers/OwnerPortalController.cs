using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerPortalController(ILeadRepository leads, IMessageDispatcher messages) : Controller
{
    [HttpGet("/owner")]
    public async Task<IActionResult> Index([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var result = await leads.SearchBySiteAsync(auth.SiteId, q, page, pageSize, ct);
        var model = new OwnerDashboardModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Query = (q ?? "").Trim(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Leads = result.Items.Select(x => new OwnerLeadListItem
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                Email = x.Email,
                CreatedAtUtc = x.CreatedAtUtc,
                Status = GetStatus(x)
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
    public async Task<IActionResult> Reply([FromRoute] Guid id, [FromForm] string? message, [FromForm] string? replyChannel, CancellationToken ct)
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

        var channel = NormalizeReplyChannel(replyChannel, lead);
        var recipient = ResolveRecipient(channel, lead);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            model.Error = GetMissingRecipientError(channel);
            model.ReplyChannel = channel;
            return View("Lead", model);
        }

        var dispatch = await messages.SendTextAsync(channel, recipient, text, ct);
        if (!dispatch.Sent)
        {
            model.Error = dispatch.Error ?? "Failed to send message.";
            model.ReplyChannel = channel;
            return View("Lead", model);
        }

        model.Success = "Reply sent.";
        model.ReplyChannel = channel;
        return View("Lead", model);
    }

    [HttpPost("/owner/leads/{id:guid}/contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContact([FromRoute] Guid id, [FromForm] string? name, [FromForm] string? email, [FromForm] string? phone, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var model = ToDetailModel(auth, lead);
        var normalizedName = NormalizeText(name);
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (!string.IsNullOrWhiteSpace(email) && normalizedEmail is null)
        {
            model.Error = "Enter a valid email address.";
            return View("Lead", model);
        }

        if (!string.IsNullOrWhiteSpace(phone) && normalizedPhone is null)
        {
            model.Error = "Enter a valid phone number.";
            return View("Lead", model);
        }

        lead.Name = normalizedName;
        lead.Email = normalizedEmail;
        lead.Phone = normalizedPhone;
        await leads.SaveAsync(lead, ct);

        var updatedModel = ToDetailModel(auth, lead);
        updatedModel.Success = "Contact details updated.";
        return View("Lead", updatedModel);
    }

    private OwnerAuthContext? GetAuthContext()
    {
        return HttpContext.Items.TryGetValue(OwnerAuthMiddleware.ContextKey, out var value)
            ? value as OwnerAuthContext
            : null;
    }

    private static OwnerLeadDetailModel ToDetailModel(OwnerAuthContext auth, Lead lead)
    {
        var inferred = InferChannel(lead);
        var defaultChannel = NormalizeReplyChannel(inferred, lead);

        return new OwnerLeadDetailModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Id = lead.Id,
            Name = lead.Name,
            Email = lead.Email,
            Phone = lead.Phone,
            Channel = lead.Channel,
            CreatedAtUtc = lead.CreatedAtUtc,
            Fields = lead.Fields,
            Conversation = lead.Conversation,
            ReplyChannel = defaultChannel,
            CanReplyViaWhatsApp = !string.IsNullOrWhiteSpace(lead.Phone),
            CanReplyViaEmail = !string.IsNullOrWhiteSpace(lead.Email)
        };
    }

    private static string? InferChannel(Lead lead)
    {
        return string.IsNullOrWhiteSpace(lead.Channel)
            ? null
            : lead.Channel.Trim().ToLowerInvariant();
    }

    private static string? ResolveRecipient(string channel, Lead lead)
    {
        return channel.ToLowerInvariant() switch
        {
            "email" => string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim(),
            _ => string.IsNullOrWhiteSpace(lead.Phone) ? null : lead.Phone.Trim()
        };
    }

    private static string GetMissingRecipientError(string channel)
    {
        return channel.ToLowerInvariant() switch
        {
            "email" => "This lead has no email address for messaging.",
            _ => "This lead has no contact number for messaging."
        };
    }

    private static string NormalizeReplyChannel(string? requestedChannel, Lead lead)
    {
        var candidate = (requestedChannel ?? "").Trim().ToLowerInvariant();
        if (candidate is "email" or "whatsapp") return candidate;

        var inferred = InferChannel(lead)?.Trim().ToLowerInvariant();
        if (inferred is "email" or "whatsapp") return inferred;

        if (!string.IsNullOrWhiteSpace(lead.Phone)) return "whatsapp";
        if (!string.IsNullOrWhiteSpace(lead.Email)) return "email";
        return "whatsapp";
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = (value ?? "").Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeEmail(string? value)
    {
        var trimmed = NormalizeText(value);
        if (trimmed is null) return null;

        try
        {
            var address = new MailAddress(trimmed);
            return string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizePhone(string? value)
    {
        var trimmed = NormalizeText(value);
        if (trimmed is null) return null;

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : null;
    }

    private static string GetStatus(LeadSummary lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Phone) || !string.IsNullOrWhiteSpace(lead.Email))
            return "Contactable";
        return "Needs Details";
    }

    public sealed class OwnerDashboardModel
    {
        public string SiteId { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public string Query { get; set; } = "";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
        public int StartItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int EndItem => Math.Min(TotalCount, Page * PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
        public List<OwnerLeadListItem> Leads { get; set; } = new();
    }

    public sealed class OwnerLeadListItem
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string Status { get; set; } = "";
    }

    public sealed class OwnerLeadDetailModel
    {
        public string SiteId { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Channel { get; set; } = "api";
        public DateTimeOffset CreatedAtUtc { get; set; }
        public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<LeadConversationTurn> Conversation { get; set; } = Array.Empty<LeadConversationTurn>();
        public string? Error { get; set; }
        public string? Success { get; set; }
        public string ReplyChannel { get; set; } = "whatsapp";
        public bool CanReplyViaWhatsApp { get; set; }
        public bool CanReplyViaEmail { get; set; }
    }
}
