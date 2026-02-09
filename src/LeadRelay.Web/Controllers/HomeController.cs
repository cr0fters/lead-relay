using LeadRelay.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class HomeController(ISiteRepository sites) : Controller
{
    [HttpGet("/")]
    public async Task<ViewResult> Index(CancellationToken ct)
    {
        var siteId = (await sites.GetAllAsync(ct)).FirstOrDefault()?.Id ?? "";
        return View(new HomeViewModel { WidgetSiteId = siteId });
    }

    public sealed class HomeViewModel
    {
        public string WidgetSiteId { get; set; } = "";
    }
}
