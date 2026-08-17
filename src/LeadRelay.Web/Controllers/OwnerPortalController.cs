using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Fields;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerPortalController(ILeadRepository leads, IMessageDispatcher messages, ISiteRepository sites) : Controller
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

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        return View(ToDetailModel(auth, lead, site));
    }

    [HttpGet("/owner/settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        if (site is null) return NotFound();

        return View(ToSettingsModel(auth, site));
    }

    [HttpPost("/owner/settings/fields")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSiteFields([FromForm] List<OwnerFieldInputModel>? fields, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        if (site is null) return NotFound();

        var parsed = ParseAndNormalizeFields(fields);
        if (parsed.Error is not null)
        {
            var invalidModel = ToSettingsModel(auth, site);
            invalidModel.Error = parsed.Error;
            return View("Settings", invalidModel);
        }

        site = new Site
        {
            Id = site.Id,
            Name = site.Name,
            BusinessSummary = site.BusinessSummary,
            AllowedDomains = site.AllowedDomains,
            Fields = parsed.Fields,
            IntroMessage = site.IntroMessage,
            OwnerEmail = site.OwnerEmail,
            WhatsAppNumber = site.WhatsAppNumber,
            WhatsAppPhoneNumberId = site.WhatsAppPhoneNumberId
        };

        await sites.UpsertAsync(site, ct);
        var updated = ToSettingsModel(auth, site);
        updated.Success = "Site field definitions updated.";
        return View("Settings", updated);
    }

    [HttpPost("/owner/leads/{id:guid}/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply([FromRoute] Guid id, [FromForm] string? message, [FromForm] string? replyChannel, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        var model = ToDetailModel(auth, lead, site);
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

        var dispatch = await messages.SendTextAsync(channel, recipient, text, auth.SiteId, ct);
        if (!dispatch.Sent)
        {
            model.Error = dispatch.Error ?? "Failed to send message.";
            model.ReplyChannel = channel;
            return View("Lead", model);
        }

        lead.Conversation.Add(new LeadConversationTurn("owner", text, DateTimeOffset.UtcNow));
        await leads.SaveAsync(lead, ct);

        var updatedModel = ToDetailModel(auth, lead, site);
        updatedModel.Success = "Reply sent.";
        updatedModel.ReplyChannel = channel;
        return View("Lead", updatedModel);
    }

    [HttpPost("/owner/leads/{id:guid}/contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContact([FromRoute] Guid id, [FromForm] string? name, [FromForm] string? email, [FromForm] string? phone, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        var model = ToDetailModel(auth, lead, site);
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

        var updatedModel = ToDetailModel(auth, lead, site);
        updatedModel.Success = "Contact details updated.";
        return View("Lead", updatedModel);
    }

    [HttpPost("/owner/leads/{id:guid}/fields")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFields([FromRoute] Guid id, [FromForm] Dictionary<string, string>? fields, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        lead.Fields.Clear();
        if (fields is not null)
        {
            foreach (var pair in fields)
            {
                var key = (pair.Key ?? "").Trim();
                var value = (pair.Value ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(key, "project_summary", StringComparison.OrdinalIgnoreCase))
                    continue;

                lead.Fields[key] = value;
            }
        }

        await leads.SaveAsync(lead, ct);
        var updatedModel = ToDetailModel(auth, lead, site);
        updatedModel.Success = "Project fields updated.";
        return View("Lead", updatedModel);
    }

    [HttpPost("/owner/leads/{id:guid}/pause")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPaused([FromRoute] Guid id, [FromForm] bool paused, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        lead.IsBotPaused = paused;
        await leads.SaveAsync(lead, ct);
        var message = paused
            ? "AI auto-reply is off."
            : "AI auto-reply is on.";

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                paused,
                message
            });
        }

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        var updatedModel = ToDetailModel(auth, lead, site);
        updatedModel.Success = message;
        return View("Lead", updatedModel);
    }

    private bool IsAjaxRequest()
    {
        var requestedWith = Request.Headers["X-Requested-With"].ToString();
        return string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private OwnerAuthContext? GetAuthContext()
    {
        return HttpContext.Items.TryGetValue(OwnerAuthMiddleware.ContextKey, out var value)
            ? value as OwnerAuthContext
            : null;
    }

    private static OwnerLeadDetailModel ToDetailModel(OwnerAuthContext auth, Lead lead, Site? site)
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
            Status = lead.Status,
            CreatedAtUtc = lead.CreatedAtUtc,
            ProjectSummary = lead.ProjectSummary,
            Fields = lead.Fields,
            FieldDefinitions = BuildFieldDefinitions(site, lead.Fields),
            Conversation = lead.Conversation,
            ReplyChannel = defaultChannel,
            CanReplyViaWhatsApp = !string.IsNullOrWhiteSpace(lead.Phone),
            CanReplyViaEmail = !string.IsNullOrWhiteSpace(lead.Email),
            IsPaused = lead.IsBotPaused
        };
    }

    private static OwnerSiteSettingsModel ToSettingsModel(OwnerAuthContext auth, Site site)
    {
        var fields = site.Fields
            .Select(x => new OwnerFieldInputModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            })
            .ToList();

        return new OwnerSiteSettingsModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            SiteName = site.Name,
            Fields = fields
        };
    }

    private static (List<ConversationField> Fields, string? Error) ParseAndNormalizeFields(List<OwnerFieldInputModel>? fields)
    {
        var mapped = (fields ?? [])
            .Select(entry => new ConversationField
            {
                Id = entry.Id ?? "",
                Name = entry.Name ?? "",
                Description = entry.Description
            })
            .ToList();

        return ConversationFieldNormalizer.Normalize(mapped);
    }

    private static IReadOnlyList<OwnerFieldDefinitionModel> BuildFieldDefinitions(
        Site? site,
        IReadOnlyDictionary<string, string> values)
    {
        var items = new List<OwnerFieldDefinitionModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in site?.Fields ?? Array.Empty<ConversationField>())
        {
            var id = (field.Id ?? "").Trim();
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;

            values.TryGetValue(id, out var value);
            items.Add(new OwnerFieldDefinitionModel
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(field.Name) ? id : field.Name,
                Description = field.Description,
                Value = value
            });
        }

        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, "project_summary", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add(pair.Key))
                continue;

            items.Add(new OwnerFieldDefinitionModel
            {
                Id = pair.Key,
                Name = pair.Key,
                Value = pair.Value
            });
        }

        return items;
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
        public string Status { get; set; } = LeadStatuses.Open;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? ProjectSummary { get; set; }
        public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<OwnerFieldDefinitionModel> FieldDefinitions { get; set; } = Array.Empty<OwnerFieldDefinitionModel>();
        public IReadOnlyList<LeadConversationTurn> Conversation { get; set; } = Array.Empty<LeadConversationTurn>();
        public string? Error { get; set; }
        public string? Success { get; set; }
        public string ReplyChannel { get; set; } = "whatsapp";
        public bool CanReplyViaWhatsApp { get; set; }
        public bool CanReplyViaEmail { get; set; }
        public bool IsPaused { get; set; }
    }

    public sealed class OwnerFieldDefinitionModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Value { get; set; }
    }

    public sealed class OwnerSiteSettingsModel
    {
        public string SiteId { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public List<OwnerFieldInputModel> Fields { get; set; } = new();
        public string? Error { get; set; }
        public string? Success { get; set; }
    }

    public sealed class OwnerFieldInputModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
