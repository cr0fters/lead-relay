using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class LegalController : Controller
{
    [HttpGet("/privacy-policy")]
    public ViewResult PrivacyPolicy()
    {
        return View("PrivacyPolicy");
    }

    [HttpGet("/terms-and-conditions")]
    public ViewResult TermsAndConditions()
    {
        return View("TermsAndConditions");
    }

    [HttpGet("/user-data-deletion")]
    public ViewResult UserDataDeletion()
    {
        return View("UserDataDeletion");
    }
}
