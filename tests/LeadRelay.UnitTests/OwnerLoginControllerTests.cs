using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerLoginControllerTests
{
    [Test]
    public async Task login_with_valid_credentials_redirects_to_owner_home()
    {
        var passwordAuth = new FakePasswordAuthService
        {
            ValidateResult = new OwnerAuthContext("site_demo", "owner@example.com")
        };
        var controller = BuildController(passwordAuth);

        var result = await controller.LoginPost(new OwnerLoginController.OwnerLoginModel
        {
            Email = "owner@example.com",
            Password = "valid-password",
            ReturnUrl = "/owner"
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<RedirectResult>());
        Assert.That(((RedirectResult)result).Url, Is.EqualTo("/owner"));
    }

    [Test]
    public async Task forgot_password_returns_generic_message()
    {
        var passwordAuth = new FakePasswordAuthService();
        var controller = BuildController(passwordAuth);

        var result = await controller.ForgotPasswordPost(
            new OwnerLoginController.ForgotPasswordModel { Email = "owner@example.com" },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = (OwnerLoginController.ForgotPasswordModel)((ViewResult)result).Model!;
        Assert.That(model.Info, Is.EqualTo("If that email exists, we sent reset instructions."));
    }

    [Test]
    public async Task reset_password_with_mismatch_returns_error()
    {
        var controller = BuildController(new FakePasswordAuthService());

        var result = await controller.ResetPasswordPost(new OwnerLoginController.ResetPasswordModel
        {
            Email = "owner@example.com",
            Token = "token",
            NewPassword = "password1",
            ConfirmPassword = "password2"
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = (OwnerLoginController.ResetPasswordModel)((ViewResult)result).Model!;
        Assert.That(model.Error, Is.EqualTo("Passwords do not match."));
    }

    private static OwnerLoginController BuildController(FakePasswordAuthService passwordAuth)
    {
        var sessions = new OwnerSessionService(
            Microsoft.Extensions.Options.Options.Create(new OwnerPortalOptions
            {
                SigningSecret = "test-secret",
                SessionCookieName = "leadrelay_owner_session",
                SessionTtlHours = 12,
                PasswordResetTtlMinutes = 30
            }),
            new LeadRelay.Infrastructure.Persistence.InMemorySiteRepository());

        var controller = new OwnerLoginController(sessions, passwordAuth)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("leadrelay.test");
        return controller;
    }

    private sealed class FakePasswordAuthService : IOwnerPasswordAuthService
    {
        public OwnerAuthContext? ValidateResult { get; set; }

        public Task<OwnerAuthContext?> ValidateCredentialsAsync(string? email, string? password, CancellationToken ct)
            => Task.FromResult(ValidateResult);

        public Task RequestPasswordResetAsync(string? email, Func<string, string> resetUrlFactory, string? userAgent, CancellationToken ct)
            => Task.CompletedTask;

        public Task<bool> ResetPasswordAsync(string? email, string? token, string? newPassword, CancellationToken ct)
            => Task.FromResult(true);
    }
}
