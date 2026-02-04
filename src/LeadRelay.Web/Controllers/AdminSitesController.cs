using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

[ApiController]
[Route("admin/api/sites")]
public sealed class AdminSitesController(ISiteRepository sites) : ControllerBase
{
    [HttpGet("{siteId}")]
    public async Task<IActionResult> Get([FromRoute] string siteId, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null) return NotFound();

        return Ok(SiteConfigResponse.FromSite(site));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SiteConfigRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            return BadRequest(new { error = "Site id is required." });

        var existing = await sites.GetByIdAsync(request.Id, ct);
        if (existing is not null) return Conflict(new { error = "Site already exists." });

        var site = request.ToSite(request.Id);
        if (!site.IsValid(out var error)) return BadRequest(new { error });

        await sites.UpsertAsync(site, ct);
        return CreatedAtAction(nameof(Get), new { siteId = site.Id }, SiteConfigResponse.FromSite(site));
    }

    [HttpPut("{siteId}")]
    public async Task<IActionResult> Update([FromRoute] string siteId, [FromBody] SiteConfigRequest request, CancellationToken ct)
    {
        var existing = await sites.GetByIdAsync(siteId, ct);
        if (existing is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Id) && !string.Equals(request.Id, siteId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Route site id does not match payload." });

        var site = request.ToSite(siteId);
        if (!site.IsValid(out var error)) return BadRequest(new { error });

        await sites.UpsertAsync(site, ct);
        return Ok(SiteConfigResponse.FromSite(site));
    }

    public sealed record SiteConfigRequest
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? BusinessSummary { get; init; }
        public List<string>? AllowedDomains { get; init; }
        public List<ConversationFieldRequest>? Fields { get; init; }
        public List<ConversationFieldRequest>? OptionalFields { get; init; }
        public string? IntroMessage { get; init; }
        public string? OwnerEmail { get; init; }
        public string? WhatsAppNumber { get; init; }

        public Site ToSite(string id)
        {
            return new Site
            {
                Id = id.Trim(),
                Name = Name?.Trim() ?? "",
                BusinessSummary = string.IsNullOrWhiteSpace(BusinessSummary) ? null : BusinessSummary.Trim(),
                AllowedDomains = (AllowedDomains ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList(),
                Fields = (Fields ?? [])
                    .Select(x => x.ToField())
                    .ToList(),
                OptionalFields = (OptionalFields ?? [])
                    .Select(x => x.ToField())
                    .ToList(),
                IntroMessage = string.IsNullOrWhiteSpace(IntroMessage) ? null : IntroMessage.Trim(),
                OwnerEmail = OwnerEmail?.Trim() ?? "",
                WhatsAppNumber = WhatsAppNumber?.Trim() ?? ""
            };
        }
    }

    public sealed record ConversationFieldRequest
    {
        public string? Key { get; init; }
        public string? Prompt { get; init; }
        public bool Required { get; init; } = true;
        public ConversationFieldType Type { get; init; } = ConversationFieldType.Text;

        public ConversationField ToField()
        {
            return new ConversationField
            {
                Key = Key?.Trim() ?? "",
                Prompt = Prompt?.Trim() ?? "",
                Required = Required,
                Type = Type
            };
        }
    }

    public sealed record SiteConfigResponse(
        string Id,
        string Name,
        string? BusinessSummary,
        IReadOnlyList<string> AllowedDomains,
        IReadOnlyList<ConversationField> Fields,
        IReadOnlyList<ConversationField> OptionalFields,
        string? IntroMessage,
        string OwnerEmail,
        string WhatsAppNumber)
    {
        public static SiteConfigResponse FromSite(Site site)
        {
            return new SiteConfigResponse(
                site.Id,
                site.Name,
                site.BusinessSummary,
                site.AllowedDomains,
                site.Fields,
                site.OptionalFields,
                site.IntroMessage,
                site.OwnerEmail,
                site.WhatsAppNumber);
        }
    }
}
