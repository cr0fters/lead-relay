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
        var siteId = Guid.NewGuid().ToString("D");
        var site = request.ToSite(siteId);
        if (!site.IsValid(out var error)) return BadRequest(new { error });

        await sites.UpsertAsync(site, ct);
        return CreatedAtAction(nameof(Get), new { siteId = site.Id }, SiteConfigResponse.FromSite(site));
    }

    [HttpPut("{siteId}")]
    public async Task<IActionResult> Update([FromRoute] string siteId, [FromBody] SiteConfigRequest request, CancellationToken ct)
    {
        var existing = await sites.GetByIdAsync(siteId, ct);
        if (existing is null) return NotFound();

        var site = request.ToSite(siteId);
        if (!site.IsValid(out var error)) return BadRequest(new { error });

        await sites.UpsertAsync(site, ct);
        return Ok(SiteConfigResponse.FromSite(site));
    }

    public sealed record SiteConfigRequest
    {
        public string? Name { get; init; }
        public string? BusinessSummary { get; init; }
        public List<string>? AllowedDomains { get; init; }
        public List<ConversationFieldRequest>? Fields { get; init; }
        public string? IntroMessage { get; init; }
        public string? OwnerEmail { get; init; }
        public string? WhatsAppNumber { get; init; }
        public string? WhatsAppPhoneNumberId { get; init; }

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
                IntroMessage = string.IsNullOrWhiteSpace(IntroMessage) ? null : IntroMessage.Trim(),
                OwnerEmail = OwnerEmail?.Trim() ?? "",
                WhatsAppNumber = WhatsAppNumber?.Trim() ?? "",
                WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(WhatsAppPhoneNumberId) ? null : WhatsAppPhoneNumberId.Trim()
            };
        }
    }

    public sealed record ConversationFieldRequest
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }

        public ConversationField ToField()
        {
            return new ConversationField
            {
                Id = Id?.Trim() ?? "",
                Name = Name?.Trim() ?? "",
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
            };
        }
    }

    public sealed record SiteConfigResponse(
        string Id,
        string Name,
        string? BusinessSummary,
        IReadOnlyList<string> AllowedDomains,
        IReadOnlyList<ConversationField> Fields,
        string? IntroMessage,
        string OwnerEmail,
        string WhatsAppNumber,
        string? WhatsAppPhoneNumberId)
    {
        public static SiteConfigResponse FromSite(Site site)
        {
            return new SiteConfigResponse(
                site.Id,
                site.Name,
                site.BusinessSummary,
                site.AllowedDomains,
                site.Fields,
                site.IntroMessage,
                site.OwnerEmail,
                site.WhatsAppNumber,
                site.WhatsAppPhoneNumberId);
        }
    }
}
