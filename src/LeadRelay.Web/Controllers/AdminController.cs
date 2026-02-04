using System.Text.Json;
using System.Text.Json.Serialization;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class AdminController(ISiteRepository sites) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await sites.GetAllAsync(ct);
        var model = new AdminDashboardModel
        {
            Sites = items
                .Select(x => new SiteSummary(x.Id, x.Name, x.OwnerEmail))
                .ToList()
        };

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
    public async Task<IActionResult> CreateSite([FromForm] SiteFormModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            model.Error = "Site id is required.";
            return View("Site", model);
        }

        var existing = await sites.GetByIdAsync(model.Id.Trim(), ct);
        if (existing is not null)
        {
            model.Error = "A site with this id already exists.";
            return View("Site", model);
        }

        if (!TryBuildSite(model, model.Id.Trim(), out var site, out var error))
        {
            model.Error = error;
            return View("Site", model);
        }

        await sites.UpsertAsync(site, ct);
        return Redirect($"/admin/sites/{site.Id}");
    }

    [HttpPost("/admin/sites/{siteId}")]
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

        await sites.UpsertAsync(site, ct);
        return Redirect($"/admin/sites/{site.Id}");
    }

    private static bool TryBuildSite(SiteFormModel model, string id, out Site site, out string error)
    {
        var allowedDomains = SplitList(model.AllowedDomains);

        if (!TryParseFields(model.FieldsJson, out var fields, out var fieldsError))
        {
            error = $"Required fields JSON invalid: {fieldsError}";
            site = null!;
            return false;
        }

        if (!TryParseFields(model.OptionalFieldsJson, out var optionalFields, out var optionalError))
        {
            error = $"Optional fields JSON invalid: {optionalError}";
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
            OptionalFields = optionalFields,
            IntroMessage = string.IsNullOrWhiteSpace(model.IntroMessage) ? null : model.IntroMessage.Trim(),
            OwnerEmail = model.OwnerEmail?.Trim() ?? "",
            WhatsAppNumber = model.WhatsAppNumber?.Trim() ?? ""
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
            fields = items;
            error = "";
            return true;
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

    public sealed class AdminDashboardModel
    {
        public List<SiteSummary> Sites { get; init; } = new();
    }

    public sealed record SiteSummary(string Id, string Name, string OwnerEmail);

    public sealed class SiteFormModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? BusinessSummary { get; set; }
        public string? AllowedDomains { get; set; }
        public string? FieldsJson { get; set; }
        public string? OptionalFieldsJson { get; set; }
        public string? IntroMessage { get; set; }
        public string? OwnerEmail { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Error { get; set; }

        public static SiteFormModel New()
        {
            return new SiteFormModel
            {
                FieldsJson = """
                             [
                               { "key": "project_description", "prompt": "Tell me a little about your project." }
                             ]
                             """
            };
        }

        public static SiteFormModel FromSite(Site site)
        {
            return new SiteFormModel
            {
                Id = site.Id,
                Name = site.Name,
                BusinessSummary = site.BusinessSummary,
                AllowedDomains = string.Join("\n", site.AllowedDomains),
                FieldsJson = JsonSerializer.Serialize(site.Fields, JsonOptions),
                OptionalFieldsJson = JsonSerializer.Serialize(site.OptionalFields, JsonOptions),
                IntroMessage = site.IntroMessage,
                OwnerEmail = site.OwnerEmail,
                WhatsAppNumber = site.WhatsAppNumber
            };
        }
    }
}
