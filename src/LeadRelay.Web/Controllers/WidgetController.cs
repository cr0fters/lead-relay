using System.Text.Json;
using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class WidgetController(ISiteRepository sites, ILogger<WidgetController> logger) : Controller
{
    [HttpGet("/widget/bootstrap.js")]
    public async Task<IActionResult> Bootstrap([FromQuery] string siteId, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null) return Unauthorized();

        var referer = Request.Headers.Referer.ToString();
        var originHeader = Request.Headers.Origin.ToString();
        if (!DomainAllowList.IsAllowedDomain(site.AllowedDomains, referer, originHeader, Request.Host.Host))
        {
            var allowedList = site.AllowedDomains.Count == 0 ? "<any>" : string.Join(", ", site.AllowedDomains);
            logger.LogWarning(
                "Widget bootstrap blocked for site {SiteId}. Referer={Referer} Origin={Origin} AllowedDomains={AllowedDomains}",
                site.Id,
                string.IsNullOrWhiteSpace(referer) ? "<empty>" : referer,
                string.IsNullOrWhiteSpace(originHeader) ? "<empty>" : originHeader,
                allowedList);

            Response.Headers.CacheControl = "no-store";
            return Content(
                $"console.warn(\"LeadRelay: widget blocked for site '{site.Id}'. Domain not in allow-list.\");",
                "application/javascript; charset=utf-8");
        }

        var origin = $"{Request.Scheme}://{Request.Host}";
        Response.Headers.CacheControl = "public, max-age=300, stale-while-revalidate=600";
        return Content($$"""
                            (function(){
                                window.__LeadRelayWidgetConfig = {{JsonSerializer.Serialize(new
                                {
                                    siteId = site.Id,
                                    apiBase = origin,
                                    waNumber = site.WhatsAppNumber,
                                    label = "Chat via WhatsApp",
                                    colour = "#25D366",
                                    position = "right",
                                    offset = 24,
                                    zIndex = 2147483000,
                                    prefill = "Hi",
                                    logoUrl = $"{origin}/widget/whatsapp-white.svg",
                                    runtimeUrl = $"{origin}/widget/wa-runtime.v1.js"
                                })}};
                                var d=document;
                                var s=d.createElement('script');
                                s.src=window.__LeadRelayWidgetConfig.runtimeUrl;
                                s.async=true;
                                d.head.appendChild(s);
                            })();
                         """, "application/javascript; charset=utf-8");
    }

    
}
