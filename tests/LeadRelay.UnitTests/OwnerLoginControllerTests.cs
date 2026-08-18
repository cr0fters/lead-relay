using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Test]
    public async Task register_with_valid_input_sends_verification_and_redirects_to_verify_email()
    {
        OwnerRegistrationRequest? request = null;
        var verification = new FakeEmailVerificationService();
        var controller = BuildController(
            new FakePasswordAuthService(),
            new FakeOwnerRegistrationService
            {
                OnRegister = r => request = r,
                Result = OwnerRegistrationResult.Success(new OwnerAuthContext("site_new", "new-owner@example.com"))
            },
            verification);

        var result = await controller.RegisterPost(new OwnerLoginController.RegisterModel
        {
            BusinessType = "interior_design",
            SiteName = "New Site",
            PayloadJson = """
                          {
                            "businessSummary": "Interior design studio for family homes.",
                            "fields": [
                              { "id": "timeline", "name": "Timeline", "description": "When do you want to start?" }
                            ]
                          }
                          """,
            Email = "new-owner@example.com",
            Password = "secure-pass",
            ConfirmPassword = "secure-pass",
            ReturnUrl = "/owner"
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<RedirectResult>());
        Assert.That(((RedirectResult)result).Url, Is.EqualTo("/owner/verify-email?sent=1"));
        Assert.That(verification.RequestedSiteIds, Is.EqualTo(new[] { "site_new" }));
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.BusinessSummary, Is.EqualTo("Interior design studio for family homes."));
        Assert.That(request.Fields, Is.Not.Null);
        Assert.That(request.Fields!.Count, Is.EqualTo(1));
        Assert.That(request.Fields[0].Id, Is.EqualTo("timeline"));
    }

    [Test]
    public async Task login_with_unverified_email_redirects_to_verification()
    {
        var passwordAuth = new FakePasswordAuthService
        {
            ValidateResult = new OwnerAuthContext("site_demo", "owner@example.com")
        };
        var verification = new FakeEmailVerificationService { IsVerified = false };
        var controller = BuildController(passwordAuth, new FakeOwnerRegistrationService(), verification);

        var result = await controller.LoginPost(new OwnerLoginController.OwnerLoginModel
        {
            Email = "owner@example.com",
            Password = "valid-password",
            ReturnUrl = "/owner"
        }, CancellationToken.None);

        Assert.That(((RedirectResult)result).Url, Is.EqualTo("/owner/verify-email"));
    }

    [Test]
    public async Task register_with_mismatch_returns_error()
    {
        var controller = BuildController(new FakePasswordAuthService());

        var result = await controller.RegisterPost(new OwnerLoginController.RegisterModel
        {
            SiteName = "New Site",
            Email = "new-owner@example.com",
            Password = "password1",
            ConfirmPassword = "password2"
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = (OwnerLoginController.RegisterModel)((ViewResult)result).Model!;
        Assert.That(model.Error, Is.EqualTo("Passwords do not match."));
    }

    private static OwnerLoginController BuildController(FakePasswordAuthService passwordAuth)
        => BuildController(passwordAuth, new FakeOwnerRegistrationService(), new FakeEmailVerificationService());

    private static OwnerLoginController BuildController(
        FakePasswordAuthService passwordAuth,
        FakeOwnerRegistrationService? registration,
        FakeEmailVerificationService? verification = null)
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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PublicBaseUrl"] = "https://leadrelay.test" })
            .Build();
        var controller = new OwnerLoginController(
            sessions,
            passwordAuth,
            registration ?? new FakeOwnerRegistrationService(),
            verification ?? new FakeEmailVerificationService(),
            configuration,
            NullLogger<OwnerLoginController>.Instance)
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

    private sealed class FakeEmailVerificationService : IOwnerEmailVerificationService
    {
        public bool IsVerified { get; set; } = true;
        public bool SendResult { get; set; } = true;
        public List<string> RequestedSiteIds { get; } = [];

        public Task<bool> IsVerifiedAsync(string siteId, CancellationToken ct)
            => Task.FromResult(IsVerified);

        public Task<bool> RequestAsync(string siteId, Func<string, string> verificationUrlFactory, CancellationToken ct)
        {
            RequestedSiteIds.Add(siteId);
            _ = verificationUrlFactory("verification-token");
            return Task.FromResult(SendResult);
        }

        public Task<bool> VerifyAsync(string? email, string? token, CancellationToken ct)
            => Task.FromResult(IsVerified);
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

    private sealed class FakeOwnerRegistrationService : IOwnerRegistrationService
    {
        public Action<OwnerRegistrationRequest>? OnRegister { get; set; }
        public OwnerRegistrationResult Result { get; set; } = OwnerRegistrationResult.Failure("not configured");

        public Task<OwnerRegistrationResult> RegisterAsync(OwnerRegistrationRequest request, CancellationToken ct)
        {
            OnRegister?.Invoke(request);
            return Task.FromResult(Result);
        }
    }
}
