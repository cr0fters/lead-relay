using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerLoginController(
    OwnerSessionService sessions,
    IOwnerPasswordAuthService passwordAuth) : Controller
{
    [HttpGet("/owner/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null, [FromQuery] string? reset = null)
    {
        if (!sessions.IsConfigured)
            return Problem("Owner portal is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        return View(new OwnerLoginModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl),
            Info = string.Equals(reset, "ok", StringComparison.OrdinalIgnoreCase)
                ? "Password updated. You can now sign in."
                : null
        });
    }

    [HttpPost("/owner/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost([FromForm] OwnerLoginModel model, CancellationToken ct)
    {
        if (!sessions.IsConfigured)
            return Problem("Owner portal is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl);
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.Error = "Email and password are required.";
            return View("Login", model);
        }

        var auth = await passwordAuth.ValidateCredentialsAsync(model.Email, model.Password, ct);
        if (auth is null)
        {
            model.Error = "Invalid email or password.";
            return View("Login", model);
        }

        var sessionToken = sessions.CreateLoginToken(auth.SiteId, auth.OwnerEmail, TimeSpan.FromHours(12));
        sessions.SignIn(HttpContext, sessionToken);
        return Redirect(model.ReturnUrl);
    }

    [HttpGet("/owner/password/forgot")]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordModel());
    }

    [HttpPost("/owner/password/forgot")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordPost([FromForm] ForgotPasswordModel model, CancellationToken ct)
    {
        var email = (model.Email ?? "").Trim();
        var normalizedEmail = Uri.EscapeDataString(email);

        await passwordAuth.RequestPasswordResetAsync(email, token =>
            $"{Request.Scheme}://{Request.Host}/owner/password/reset?email={normalizedEmail}&token={Uri.EscapeDataString(token)}", ct);

        model.Info = "If that email exists, we sent reset instructions.";
        return View("ForgotPassword", model);
    }

    [HttpGet("/owner/password/reset")]
    public IActionResult ResetPassword([FromQuery] string? email = null, [FromQuery] string? token = null)
    {
        return View(new ResetPasswordModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost("/owner/password/reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordPost([FromForm] ResetPasswordModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Token) ||
            string.IsNullOrWhiteSpace(model.NewPassword) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            model.Error = "All fields are required.";
            return View("ResetPassword", model);
        }

        if (!string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
        {
            model.Error = "Passwords do not match.";
            return View("ResetPassword", model);
        }

        if (model.NewPassword.Length < 8)
        {
            model.Error = "Password must be at least 8 characters.";
            return View("ResetPassword", model);
        }

        var ok = await passwordAuth.ResetPasswordAsync(model.Email, model.Token, model.NewPassword, ct);
        if (!ok)
        {
            model.Error = "Invalid or expired reset token.";
            return View("ResetPassword", model);
        }

        return Redirect("/owner/login?reset=ok");
    }

    [HttpPost("/owner/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        sessions.SignOut(HttpContext);
        return Redirect("/owner/login");
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/owner";
        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _)) return "/owner";
        if (!returnUrl.StartsWith('/')) return "/owner";
        if (returnUrl.StartsWith("//", StringComparison.Ordinal)) return "/owner";

        return returnUrl;
    }

    public sealed class OwnerLoginModel
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string ReturnUrl { get; set; } = "/owner";
        public string? Error { get; set; }
        public string? Info { get; set; }
    }

    public sealed class ForgotPasswordModel
    {
        public string? Email { get; set; }
        public string? Info { get; set; }
    }

    public sealed class ResetPasswordModel
    {
        public string? Email { get; set; }
        public string? Token { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? Error { get; set; }
    }
}
