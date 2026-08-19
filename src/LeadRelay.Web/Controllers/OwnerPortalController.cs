using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Fields;
using LeadRelay.Web.Leads;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net.Mail;
using System.Text;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerPortalController(
    ILeadRepository leads,
    IMessageDispatcher messages,
    ISiteRepository sites,
    IClock clock) : Controller
{
    [HttpGet("/owner")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var normalizedStage = NormalizeStageFilter(stage);
        var fromDate = ParseDateFilter(from);
        var toDate = ParseDateFilter(to);
        var filterError = GetFilterError(stage, normalizedStage, from, fromDate, to, toDate);
        var fromUtc = fromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? beforeUtc = null;
        if (toDate is not null && toDate != DateOnly.MaxValue)
            beforeUtc = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        if (toDate == DateOnly.MaxValue)
            filterError = "Enter a to date earlier than 9999-12-31.";

        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            filterError = "The from date must be on or before the to date.";
            fromUtc = null;
            beforeUtc = null;
        }

        var result = await leads.SearchBySiteAsync(
            auth.SiteId,
            new LeadSearchCriteria(q, normalizedStage, fromUtc, beforeUtc, page, pageSize),
            ct);
        var model = new OwnerDashboardModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Query = (q ?? "").Trim(),
            Stage = normalizedStage ?? "",
            FromDate = (from ?? "").Trim(),
            ToDate = (to ?? "").Trim(),
            Error = filterError,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            NewCount = result.NewCount,
            Leads = result.Items.Select(x => new OwnerLeadListItem
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                Email = x.Email,
                CreatedAtUtc = x.CreatedAtUtc,
                ProjectStage = ProjectStatuses.Normalize(x.ProjectStage),
                IsNew = x.IsNew,
                Channel = x.Channel,
                IsTest = x.IsTest
            }).ToList()
        };

        return View(model);
    }

    [HttpGet("/owner/leads/export.csv")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        if (site is null) return NotFound();

        var rows = await leads.GetExportBySiteAsync(auth.SiteId, ct);
        var csv = LeadCsvExporter.Export(rows, site.Fields);
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);

        return File(
            content,
            "text/csv; charset=utf-8",
            $"leadrelay-leads-{DateTimeOffset.UtcNow:yyyyMMdd}.csv");
    }

    [HttpPost("/owner/leads/{id:guid}/stage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStage(
        [FromRoute] Guid id,
        [FromForm] string? stage,
        CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var normalizedStage = (stage ?? "").Trim().ToLowerInvariant();
        if (!ProjectStatuses.IsOwnerStage(normalizedStage))
        {
            var invalidLead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
            if (invalidLead is null) return NotFound();

            var invalidSite = await sites.GetByIdAsync(auth.SiteId, ct);
            var invalidModel = ToDetailModel(auth, invalidLead, invalidSite);
            invalidModel.Error = "Choose a valid lead stage.";
            return View("Lead", invalidModel);
        }

        var updated = await leads.UpdateProjectStageAsync(
            id,
            auth.SiteId,
            normalizedStage,
            DateTimeOffset.UtcNow,
            ct);
        if (!updated) return NotFound();

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        var model = ToDetailModel(auth, lead, site);
        model.Success = $"Lead moved to {GetStageLabel(normalizedStage)}.";
        return View("Lead", model);
    }

    [HttpPost("/owner/leads/{id:guid}/follow-up")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFollowUp(
        [FromRoute] Guid id,
        [FromForm] string? ownerNotes,
        [FromForm] string? nextAction,
        [FromForm] string? nextActionAtUtc,
        CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        var model = ToDetailModel(auth, lead, site);
        var normalizedNotes = NormalizeText(ownerNotes);
        var normalizedAction = NormalizeText(nextAction);
        var parsedDueAt = ParseUtcDateTime(nextActionAtUtc);
        model.OwnerNotes = normalizedNotes;
        model.NextAction = normalizedAction;
        model.NextActionAtInput = (nextActionAtUtc ?? "").Trim();

        if (normalizedNotes?.Length > 4000)
        {
            model.Error = "Notes must be 4,000 characters or fewer.";
            return View("Lead", model);
        }
        if (normalizedAction?.Length > 500)
        {
            model.Error = "Next action must be 500 characters or fewer.";
            return View("Lead", model);
        }
        if (!string.IsNullOrWhiteSpace(nextActionAtUtc) && parsedDueAt is null)
        {
            model.Error = "Enter a valid due date and time.";
            return View("Lead", model);
        }
        if (normalizedAction is null && parsedDueAt is not null)
        {
            model.Error = "Add a next action before setting its due date.";
            return View("Lead", model);
        }

        var updated = await leads.UpdateProjectFollowUpAsync(
            id,
            auth.SiteId,
            normalizedNotes,
            normalizedAction,
            parsedDueAt,
            DateTimeOffset.UtcNow,
            ct);
        if (!updated) return NotFound();

        var refreshed = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (refreshed is null) return NotFound();

        var updatedModel = ToDetailModel(auth, refreshed, site);
        updatedModel.Success = "Notes and next action updated.";
        return View("Lead", updatedModel);
    }

    [HttpGet("/owner/leads/{id:guid}")]
    public async Task<IActionResult> Lead([FromRoute] Guid id, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var lead = await leads.GetByIdForSiteAsync(id, auth.SiteId, ct);
        if (lead is null) return NotFound();

        var viewedAtUtc = DateTimeOffset.UtcNow;
        var markedViewed = await leads.MarkViewedAsync(id, auth.SiteId, viewedAtUtc, ct);
        if (!markedViewed) return NotFound();
        lead.OwnerViewedAtUtc ??= viewedAtUtc;

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

        if (string.Equals(channel, "whatsapp", StringComparison.OrdinalIgnoreCase) &&
            !WhatsAppCustomerServiceWindow.Evaluate(lead.Conversation, clock.UtcNow).IsOpen)
        {
            model.Error = "The 24-hour WhatsApp customer-service window has closed. Ask the customer to message again or reply by email. An approved WhatsApp template is required otherwise, and LeadRelay template sending is not available yet.";
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

        lead.Conversation.Add(new LeadConversationTurn("owner", text, clock.UtcNow));
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

    private OwnerLeadDetailModel ToDetailModel(OwnerAuthContext auth, Lead lead, Site? site)
    {
        var inferred = InferChannel(lead);
        var defaultChannel = NormalizeReplyChannel(inferred, lead);
        var whatsAppWindow = WhatsAppCustomerServiceWindow.Evaluate(lead.Conversation, clock.UtcNow);
        if (string.Equals(defaultChannel, "whatsapp", StringComparison.OrdinalIgnoreCase) &&
            !whatsAppWindow.IsOpen &&
            !string.IsNullOrWhiteSpace(lead.Email))
        {
            defaultChannel = "email";
        }

        return new OwnerLeadDetailModel
        {
            SiteId = auth.SiteId,
            OwnerEmail = auth.OwnerEmail,
            Id = lead.Id,
            Name = lead.Name,
            Email = lead.Email,
            Phone = lead.Phone,
            Channel = lead.Channel,
            IsTest = lead.IsTest,
            ProjectStage = ProjectStatuses.Normalize(lead.ProjectStage),
            StageOptions = ProjectStatuses.OwnerStages
                .Select(x => new OwnerStageOptionModel { Value = x, Label = GetStageLabel(x) })
                .ToList(),
            CreatedAtUtc = lead.CreatedAtUtc,
            ProjectSummary = lead.ProjectSummary,
            OwnerNotes = lead.OwnerNotes,
            NextAction = lead.NextAction,
            NextActionAtUtc = lead.NextActionAtUtc,
            NextActionAtInput = lead.NextActionAtUtc?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? "",
            Fields = lead.Fields,
            FieldDefinitions = BuildFieldDefinitions(site, lead.Fields),
            Conversation = lead.Conversation,
            Activity = BuildActivity(lead),
            ReplyChannel = defaultChannel,
            CanReplyViaWhatsApp = !string.IsNullOrWhiteSpace(lead.Phone),
            CanReplyViaEmail = !string.IsNullOrWhiteSpace(lead.Email),
            IsWhatsAppWindowOpen = whatsAppWindow.IsOpen,
            WhatsAppWindowEndsAtUtc = whatsAppWindow.ClosesAtUtc,
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

    private static DateTimeOffset? ParseUtcDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return DateTime.TryParseExact(
            value.Trim(),
            "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? new DateTimeOffset(parsed)
            : null;
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

    private static IReadOnlyList<OwnerLeadActivityModel> BuildActivity(Lead lead)
    {
        var messages = lead.Conversation.Select(turn => new OwnerLeadActivityModel
        {
            Kind = "message",
            Role = turn.Role,
            Text = turn.Text,
            AtUtc = turn.AtUtc
        });
        var stageChanges = lead.ProjectStageChanges.Select(change => new OwnerLeadActivityModel
        {
            Kind = "stage",
            Role = "owner",
            Text = $"Stage changed from {GetStageLabel(change.FromStage)} to {GetStageLabel(change.ToStage)}.",
            AtUtc = change.AtUtc
        });

        return messages.Concat(stageChanges)
            .OrderBy(x => x.AtUtc)
            .ToList();
    }

    private static string? NormalizeStageFilter(string? stage)
    {
        var normalized = (stage ?? "").Trim().ToLowerInvariant();
        return ProjectStatuses.IsOwnerStage(normalized) ? normalized : null;
    }

    private static DateOnly? ParseDateFilter(string? value)
    {
        return DateOnly.TryParseExact(
            (value ?? "").Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? GetFilterError(
        string? requestedStage,
        string? normalizedStage,
        string? requestedFrom,
        DateOnly? parsedFrom,
        string? requestedTo,
        DateOnly? parsedTo)
    {
        if (!string.IsNullOrWhiteSpace(requestedStage) && normalizedStage is null)
            return "Choose a valid lead stage.";
        if (!string.IsNullOrWhiteSpace(requestedFrom) && parsedFrom is null)
            return "Enter a valid from date.";
        if (!string.IsNullOrWhiteSpace(requestedTo) && parsedTo is null)
            return "Enter a valid to date.";
        return null;
    }

    private static string GetStageLabel(string? stage)
    {
        return ProjectStatuses.Normalize(stage) switch
        {
            ProjectStatuses.Qualified => "Qualified",
            ProjectStatuses.Contacted => "Contacted",
            ProjectStatuses.Won => "Won",
            ProjectStatuses.Lost => "Lost",
            _ => "New"
        };
    }

    public sealed class OwnerDashboardModel
    {
        public string SiteId { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public string Query { get; set; } = "";
        public string Stage { get; set; } = "";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string? Error { get; set; }
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Query) ||
            !string.IsNullOrWhiteSpace(Stage) ||
            !string.IsNullOrWhiteSpace(FromDate) ||
            !string.IsNullOrWhiteSpace(ToDate);
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
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
        public string ProjectStage { get; set; } = ProjectStatuses.New;
        public bool IsNew { get; set; }
        public string Channel { get; set; } = "api";
        public bool IsTest { get; set; }
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
        public bool IsTest { get; set; }
        public string ProjectStage { get; set; } = ProjectStatuses.New;
        public IReadOnlyList<OwnerStageOptionModel> StageOptions { get; set; } = Array.Empty<OwnerStageOptionModel>();
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? ProjectSummary { get; set; }
        public string? OwnerNotes { get; set; }
        public string? NextAction { get; set; }
        public DateTimeOffset? NextActionAtUtc { get; set; }
        public string NextActionAtInput { get; set; } = "";
        public IReadOnlyDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<OwnerFieldDefinitionModel> FieldDefinitions { get; set; } = Array.Empty<OwnerFieldDefinitionModel>();
        public IReadOnlyList<LeadConversationTurn> Conversation { get; set; } = Array.Empty<LeadConversationTurn>();
        public IReadOnlyList<OwnerLeadActivityModel> Activity { get; set; } = Array.Empty<OwnerLeadActivityModel>();
        public string? Error { get; set; }
        public string? Success { get; set; }
        public string ReplyChannel { get; set; } = "whatsapp";
        public bool CanReplyViaWhatsApp { get; set; }
        public bool CanReplyViaEmail { get; set; }
        public bool IsWhatsAppWindowOpen { get; set; }
        public DateTimeOffset? WhatsAppWindowEndsAtUtc { get; set; }
        public bool CanSendReply => (CanReplyViaWhatsApp && IsWhatsAppWindowOpen) || CanReplyViaEmail;
        public bool IsPaused { get; set; }
    }

    public sealed class OwnerStageOptionModel
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public sealed class OwnerLeadActivityModel
    {
        public string Kind { get; set; } = "message";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTimeOffset AtUtc { get; set; }
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
