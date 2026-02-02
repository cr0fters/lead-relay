using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class ErrorController : Controller
{
    [Route("/error")]
    public IActionResult Index() => Problem("An unexpected error occurred.");
}
