using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public ViewResult Index(CancellationToken ct)
    {
        ViewData["Title"] = "LeadRelay | WhatsApp lead qualification and lightweight CRM";
        ViewData["Description"] = "Capture website enquiries in WhatsApp, qualify them with your own questions, and manage every structured lead in one lightweight CRM inbox.";
        ViewData["CanonicalUrl"] = "https://leadrelay.dev/";
        ViewData["OpenGraphImage"] = "https://leadrelay.dev/brand/leadrelay-icon-square-1024.png";
        return View();
    }
}
