using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public ViewResult Index()
    {
        return View();
    }
}
