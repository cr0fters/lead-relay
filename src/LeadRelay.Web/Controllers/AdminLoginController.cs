using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Controllers;

public sealed class AdminLoginController(IOptions<AdminAuthOptions> options) : Controller
{
    private readonly AdminAuthOptions _options = options.Value;

    [HttpGet("/admin/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
            return Problem("Admin token is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        return View(new AdminLoginModel { ReturnUrl = NormalizeReturnUrl(returnUrl) });
    }

    [HttpPost("/admin/login")]
    [ValidateAntiForgeryToken]
    public IActionResult LoginPost([FromForm] AdminLoginModel model)
    {
        var configuredToken = _options.Token?.Trim();
        if (string.IsNullOrWhiteSpace(configuredToken))
            return Problem("Admin token is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        if (!string.Equals(configuredToken, model.Token?.Trim(), StringComparison.Ordinal))
        {
            model.Error = "Invalid admin token.";
            model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl);
            return View("Login", model);
        }

        Response.Cookies.Append(_options.CookieName, configuredToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });

        return Redirect(NormalizeReturnUrl(model.ReturnUrl));
    }

    [HttpPost("/admin/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(_options.CookieName);
        return Redirect("/admin/login");
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/admin";
        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _)) return "/admin";
        if (!returnUrl.StartsWith('/')) return "/admin";
        if (returnUrl.StartsWith("//", StringComparison.Ordinal)) return "/admin";

        return returnUrl;
    }

    public sealed class AdminLoginModel
    {
        public string? Token { get; set; }
        public string ReturnUrl { get; set; } = "/admin";
        public string? Error { get; set; }
    }
}
