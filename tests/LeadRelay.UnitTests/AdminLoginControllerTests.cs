using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class AdminLoginControllerTests
{
    [Test]
    public void login_forces_secure_admin_cookie_in_production()
    {
        var controller = CreateController();

        var result = controller.LoginPost(new AdminLoginController.AdminLoginModel
        {
            Token = "secret-token",
            ReturnUrl = "/admin"
        });

        Assert.That(result, Is.TypeOf<RedirectResult>());
        var cookie = controller.Response.Headers.SetCookie.ToString();
        Assert.That(cookie, Does.Contain("secure").IgnoreCase);
        Assert.That(cookie, Does.Contain("httponly").IgnoreCase);
        Assert.That(cookie, Does.Contain("path=/").IgnoreCase);
    }

    [Test]
    public void logout_deletes_admin_cookie_with_matching_security_attributes()
    {
        var controller = CreateController();

        var result = controller.Logout();

        Assert.That(result, Is.TypeOf<RedirectResult>());
        var cookie = controller.Response.Headers.SetCookie.ToString();
        Assert.That(cookie, Does.Contain("expires=").IgnoreCase);
        Assert.That(cookie, Does.Contain("secure").IgnoreCase);
        Assert.That(cookie, Does.Contain("samesite=lax").IgnoreCase);
        Assert.That(cookie, Does.Contain("path=/").IgnoreCase);
    }

    [Test]
    public void login_strips_queries_from_return_url_before_redirecting()
    {
        var controller = CreateController();

        var result = controller.LoginPost(new AdminLoginController.AdminLoginModel
        {
            Token = "secret-token",
            ReturnUrl = "/admin/sites/site_demo?adminToken=must-not-survive"
        });

        Assert.That(result, Is.TypeOf<RedirectResult>());
        Assert.That(((RedirectResult)result).Url, Is.EqualTo("/admin/sites/site_demo"));
    }

    private static AdminLoginController CreateController()
    {
        return new AdminLoginController(
            Options.Create(new AdminAuthOptions { Token = "secret-token" }),
            new TestWebHostEnvironment(Environments.Production))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
