using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerLoginController(
    OwnerSessionService sessions,
    IOwnerPasswordAuthService passwordAuth,
    IOwnerRegistrationService registration) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("/owner/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null, [FromQuery] string? reset = null)
    {
        if (!sessions.IsConfigured)
            return Problem("Login workspace is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        return View(new OwnerLoginModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl),
            Info = string.Equals(reset, "ok", StringComparison.OrdinalIgnoreCase)
                ? "Password updated. You can now sign in."
                : null
        });
    }

    [HttpGet("/owner/register")]
    public IActionResult Register([FromQuery] string? returnUrl = null)
    {
        if (!sessions.IsConfigured)
            return Problem("Login workspace is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        return View(new RegisterModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl)
        });
    }

    [HttpPost("/owner/register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterPost([FromForm] RegisterModel model, CancellationToken ct)
    {
        if (!sessions.IsConfigured)
            return Problem("Login workspace is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl);
        if (string.IsNullOrWhiteSpace(model.SiteName) ||
            string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            model.Error = "All fields are required.";
            return View("Register", model);
        }

        if (!string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            model.Error = "Passwords do not match.";
            return View("Register", model);
        }

        OwnerRegistrationPayload? payload;
        try
        {
            payload = string.IsNullOrWhiteSpace(model.PayloadJson)
                ? null
                : JsonSerializer.Deserialize<OwnerRegistrationPayload>(model.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        payload ??= new OwnerRegistrationPayload(null, []);

        if (!OwnerRegistrationPayloadParser.TryNormalizeFields(payload.Fields, out var fields, out var fieldError))
        {
            model.Error = fieldError ?? "Please review your field setup.";
            return View("Register", model);
        }

        var result = await registration.RegisterAsync(new OwnerRegistrationRequest(
            model.SiteName,
            payload.BusinessSummary,
            fields,
            model.Email,
            model.Password), ct);
        if (!result.Succeeded || result.Auth is null)
        {
            model.Error = result.Error ?? "Unable to create account.";
            return View("Register", model);
        }

        var sessionToken = sessions.CreateLoginToken(result.Auth.SiteId, result.Auth.OwnerEmail, TimeSpan.FromHours(12));
        sessions.SignIn(HttpContext, sessionToken);
        return Redirect(model.ReturnUrl == "/owner" ? "/owner/onboarding" : model.ReturnUrl);
    }

    [HttpPost("/owner/login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginPost([FromForm] OwnerLoginModel model, CancellationToken ct)
    {
        if (!sessions.IsConfigured)
            return Problem("Login workspace is not configured.", statusCode: StatusCodes.Status500InternalServerError);

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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPasswordPost([FromForm] ForgotPasswordModel model, CancellationToken ct)
    {
        var email = (model.Email ?? "").Trim();
        var normalizedEmail = Uri.EscapeDataString(email);
        var userAgent = Request.Headers.UserAgent.ToString();

        await passwordAuth.RequestPasswordResetAsync(email, token =>
            $"{Request.Scheme}://{Request.Host}/owner/password/reset?email={normalizedEmail}&token={Uri.EscapeDataString(token)}",
            userAgent,
            ct);

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
    [EnableRateLimiting("auth")]
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

    public sealed class RegisterModel
    {
        public string? BusinessType { get; set; }
        public string? CustomBusinessType { get; set; }
        public string? SiteName { get; set; }
        public string? PayloadJson { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string ReturnUrl { get; set; } = "/owner";
        public string? Error { get; set; }
    }
}
