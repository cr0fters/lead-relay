using System.Text.Json;
using System.Text.Json.Serialization;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Fields;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Net.Mail;
using System.Text;

namespace LeadRelay.Web.Controllers;

public sealed class AdminController(
    ISiteRepository sites,
    IHttpClientFactory httpClientFactory,
    ILogger<AdminController> logger) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await BuildDashboardModelAsync(ct);
        return View(model);
    }

    [HttpGet("/admin/sites/new")]
    public IActionResult NewSite()
    {
        return View("Site", SiteFormModel.New());
    }

    [HttpGet("/admin/sites/{siteId}")]
    public async Task<IActionResult> EditSite([FromRoute] string siteId, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null) return NotFound();

        return View("Site", SiteFormModel.FromSite(site));
    }

    [HttpPost("/admin/sites/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSite([FromForm] SiteFormModel model, CancellationToken ct)
    {
        var siteId = CreateSiteId();

        if (!TryBuildSite(model, siteId, out var site, out var error))
        {
            model.Error = error;
            return View("Site", model);
        }

        try
        {
            await sites.UpsertAsync(site, ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Admin site creation failed for site {SiteId}.", site.Id);
            model.Error = GetSiteConstraintError(exception) ?? "The site could not be saved right now. Please try again.";
            return View("Site", model);
        }
        return Redirect($"/admin/sites/{site.Id}");
    }

    [HttpPost("/admin/sites/{siteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSite([FromRoute] string siteId, [FromForm] SiteFormModel model, CancellationToken ct)
    {
        var existing = await sites.GetByIdAsync(siteId, ct);
        if (existing is null) return NotFound();

        if (!TryBuildSite(model, siteId, out var site, out var error))
        {
            model.Id = siteId;
            model.Error = error;
            return View("Site", model);
        }

        try
        {
            await sites.UpsertAsync(site, ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Admin site update failed for site {SiteId}.", site.Id);
            model.Id = siteId;
            model.Error = GetSiteConstraintError(exception) ?? "The site could not be saved right now. Please try again.";
            return View("Site", model);
        }
        return Redirect($"/admin/sites/{site.Id}");
    }

    [HttpPost("/admin/tools/email-test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendGlobalEmailTest([FromForm] EmailTestInputModel input, CancellationToken ct)
    {
        var model = await BuildDashboardModelAsync(ct);
        model.EmailTest = new AdminEmailTestModel
        {
            ApiBaseUrl = NormalizeBaseUrl(input.ApiBaseUrl),
            ServerToken = input.ServerToken?.Trim() ?? "",
            FromEmail = input.FromEmail?.Trim() ?? "",
            FromName = input.FromName?.Trim(),
            ToEmail = input.ToEmail?.Trim() ?? "",
            Subject = string.IsNullOrWhiteSpace(input.Subject) ? "LeadRelay transactional email test" : input.Subject.Trim(),
            BodyText = string.IsNullOrWhiteSpace(input.BodyText)
                ? $"LeadRelay Postmark test sent at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC."
                : input.BodyText.Trim()
        };

        if (string.IsNullOrWhiteSpace(model.EmailTest.ServerToken))
        {
            model.EmailTest.Error = "Server token is required.";
            return View("Index", model);
        }

        if (!MailAddress.TryCreate(model.EmailTest.FromEmail, out _))
        {
            model.EmailTest.Error = "Enter a valid from email address.";
            return View("Index", model);
        }

        if (!MailAddress.TryCreate(model.EmailTest.ToEmail, out _))
        {
            model.EmailTest.Error = "Enter a valid recipient email address.";
            return View("Index", model);
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(model.EmailTest.ApiBaseUrl!, UriKind.Absolute);

            var from = string.IsNullOrWhiteSpace(model.EmailTest.FromName)
                ? model.EmailTest.FromEmail
                : $"{model.EmailTest.FromName} <{model.EmailTest.FromEmail}>";

            var payload = new Dictionary<string, string>
            {
                ["From"] = from,
                ["To"] = model.EmailTest.ToEmail,
                ["Subject"] = model.EmailTest.Subject,
                ["TextBody"] = model.EmailTest.BodyText
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "email")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Postmark-Server-Token", model.EmailTest.ServerToken);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                var details = ExtractPostmarkErrorDetails(responseBody);
                logger.LogWarning(
                    "Global admin Postmark test failed with status {StatusCode}.",
                    (int)response.StatusCode);

                model.EmailTest.Error = string.IsNullOrWhiteSpace(details)
                    ? $"Postmark error {(int)response.StatusCode}. Check token, sender signature, and server settings."
                    : $"Postmark error {(int)response.StatusCode}: {details}";
                return View("Index", model);
            }

            model.EmailTest.Success = $"Test email sent to {model.EmailTest.ToEmail}.";
            return View("Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed global admin Postmark test email send to {Recipient}", model.EmailTest.ToEmail);
            model.EmailTest.Error = "Failed to send test email. Check Postmark settings and sender verification.";
            return View("Index", model);
        }
    }

    private async Task<AdminDashboardModel> BuildDashboardModelAsync(CancellationToken ct)
    {
        var items = await sites.GetAllAsync(ct);
        return new AdminDashboardModel
        {
            Sites = items
                .Select(x => new SiteSummary(x.Id, x.Name, x.OwnerEmail))
                .ToList()
        };
    }

    private static string NormalizeBaseUrl(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "https://api.postmarkapp.com/" : value.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"https://{normalized}";
        }

        return normalized.EndsWith('/') ? normalized : $"{normalized}/";
    }

    private static string? ExtractPostmarkErrorDetails(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("Message", out var message))
            {
                var text = message.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }
        catch (JsonException)
        {
        }

        var trimmed = responseBody.Trim();
        return trimmed.Length <= 220 ? trimmed : trimmed[..220];
    }

    private static bool TryBuildSite(SiteFormModel model, string id, out Site site, out string error)
    {
        var allowedDomains = SplitList(model.AllowedDomains);

        if (!TryParseFields(model.FieldsJson, out var fields, out var fieldsError))
        {
            error = $"Fields JSON invalid: {fieldsError}";
            site = null!;
            return false;
        }

        site = new Site
        {
            Id = id.Trim(),
            Name = model.Name?.Trim() ?? "",
            BusinessSummary = string.IsNullOrWhiteSpace(model.BusinessSummary) ? null : model.BusinessSummary.Trim(),
            AllowedDomains = allowedDomains,
            Fields = fields,
            IntroMessage = string.IsNullOrWhiteSpace(model.IntroMessage) ? null : model.IntroMessage.Trim(),
            OwnerEmail = model.OwnerEmail?.Trim() ?? "",
            WhatsAppNumber = model.WhatsAppNumber?.Trim() ?? "",
            WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(model.WhatsAppPhoneNumberId) ? null : model.WhatsAppPhoneNumberId.Trim()
        };

        if (!site.IsValid(out error)) return false;

        error = "";
        return true;
    }

    private static bool TryParseFields(string? json, out IReadOnlyList<ConversationField> fields, out string error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            fields = Array.Empty<ConversationField>();
            error = "";
            return true;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<ConversationField>>(json, JsonOptions) ?? new();
            var normalized = ConversationFieldNormalizer.Normalize(items);
            fields = normalized.Fields;
            error = normalized.Error ?? "";
            return normalized.Error is null;
        }
        catch (JsonException ex)
        {
            fields = Array.Empty<ConversationField>();
            error = ex.Message;
            return false;
        }
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

        var parts = value
            .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parts;
    }

    private static string CreateSiteId()
        => Guid.NewGuid().ToString("D");

    private static string? GetSiteConstraintError(DbUpdateException exception)
    {
        if (exception.InnerException is not MySqlException mysqlException ||
            mysqlException.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
        {
            return null;
        }

        if (mysqlException.Message.Contains("IX_Sites_OwnerEmail", StringComparison.OrdinalIgnoreCase))
            return "A site with that owner email already exists.";
        if (mysqlException.Message.Contains("IX_Sites_WhatsAppPhoneNumberId", StringComparison.OrdinalIgnoreCase))
            return "That WhatsApp phone number ID is already assigned to another site.";
        return null;
    }

    public sealed class AdminDashboardModel
    {
        public List<SiteSummary> Sites { get; init; } = new();
        public AdminEmailTestModel EmailTest { get; set; } = new();
    }

    public sealed record SiteSummary(string Id, string Name, string OwnerEmail);

    public sealed class AdminEmailTestModel
    {
        public string ApiBaseUrl { get; set; } = "https://api.postmarkapp.com/";
        public string ServerToken { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string? FromName { get; set; }
        public string ToEmail { get; set; } = "";
        public string Subject { get; set; } = "LeadRelay transactional email test";
        public string BodyText { get; set; } = "";
        public string? Success { get; set; }
        public string? Error { get; set; }
    }

    public sealed class EmailTestInputModel
    {
        public string? ApiBaseUrl { get; set; }
        public string? ServerToken { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
        public string? ToEmail { get; set; }
        public string? Subject { get; set; }
        public string? BodyText { get; set; }
    }

    public sealed class SiteFormModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? BusinessSummary { get; set; }
        public string? AllowedDomains { get; set; }
        public string? FieldsJson { get; set; }
        public string? IntroMessage { get; set; }
        public string? OwnerEmail { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? WhatsAppPhoneNumberId { get; set; }
        public string? Error { get; set; }

        public static SiteFormModel New()
        {
            return new SiteFormModel
            {
                FieldsJson = """
                             [
                               { "id": "project_overview", "name": "Project overview", "description": "What space is being designed and what is the main challenge?" },
                               { "id": "timeline", "name": "Timeline", "description": "When should this start?" },
                               { "id": "budget", "name": "Budget", "description": "What budget range do you have in mind?" }
                             ]
                             """
            };
        }

        public string? OwnerLoginPath { get; set; }

        public static SiteFormModel FromSite(Site site)
        {
            return new SiteFormModel
            {
                Id = site.Id,
                Name = site.Name,
                BusinessSummary = site.BusinessSummary,
                AllowedDomains = string.Join("\n", site.AllowedDomains),
                FieldsJson = JsonSerializer.Serialize(site.Fields, JsonOptions),
                IntroMessage = site.IntroMessage,
                OwnerEmail = site.OwnerEmail,
                WhatsAppNumber = site.WhatsAppNumber,
                WhatsAppPhoneNumberId = site.WhatsAppPhoneNumberId,
                OwnerLoginPath = "/owner/login"
            };
        }
    }
}
