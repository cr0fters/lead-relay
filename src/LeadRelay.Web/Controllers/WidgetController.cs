using System.Text;
using System.Text.Json;
using LeadRelay.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class WidgetController(ISiteRepository sites) : Controller
{
    [HttpGet("/widget/demo")]
    public ViewResult Demo()
    {
        return View();
    }

    [HttpGet("/widget/bootstrap.js")]
    public async Task<IActionResult> Bootstrap([FromQuery] string siteId, [FromQuery] string publicKey, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null) return Unauthorized();

        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(site.PublicKey),
                Encoding.UTF8.GetBytes(publicKey)))
            return Unauthorized();

        var origin = $"{Request.Scheme}://{Request.Host}";
        Response.Headers.CacheControl = "public, max-age=300, stale-while-revalidate=600";
        return Content($$"""
                            (function(){
                                window.__LeadRelayWidgetConfig = {{JsonSerializer.Serialize(new
                                {
                                    siteId = site.Id,
                                    publicKey,
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