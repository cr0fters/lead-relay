using LeadRelay.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public ViewResult Index(CancellationToken ct)
    {
        return View();
    }
}
