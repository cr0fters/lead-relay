using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class AdminTokenMiddlewareTests
{
    [Test]
    public async Task non_admin_path_bypasses_auth_check()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/health");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task admin_path_without_token_returns_unauthorized()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/admin/login?returnUrl=%2Fadmin"));
    }

    [Test]
    public async Task admin_api_path_without_token_returns_unauthorized()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin/api/sites/site_demo");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task admin_path_with_matching_header_token_is_authorized()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin");
        context.Request.Headers["X-Admin-Token"] = "secret-token";

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.Headers.SetCookie.ToString(), Is.Empty,
            "Header authentication must not copy the shared token into a response cookie.");
    }

    [Test]
    public async Task admin_path_with_matching_bearer_token_is_authorized()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin/api/sites/site_demo");
        context.Request.Headers.Authorization = "Bearer secret-token";

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task admin_path_with_matching_query_token_is_rejected_without_reflecting_it_into_redirect()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin?adminToken=secret-token");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/admin/login?returnUrl=%2Fadmin"));
        Assert.That(context.Response.Headers.Location.ToString(), Does.Not.Contain("secret-token"));
    }

    [Test]
    public async Task admin_api_query_token_is_rejected_without_redirect()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin/api/sites/site_demo?adminToken=secret-token");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(context.Response.Headers.Location.ToString(), Is.Empty);
    }

    [Test]
    public async Task admin_path_with_matching_cookie_token_is_authorized()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin");
        context.Request.Headers.Cookie = "leadrelay_admin_token=secret-token";

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task admin_path_is_denied_when_configured_token_missing()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true, new AdminAuthOptions { Token = "" });
        var context = CreateContext("/admin");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
    }

    [Test]
    public async Task login_path_is_allowed_without_token()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext("/admin/login");

        await middleware.Invoke(context);

        Assert.That(nextCalled, Is.True);
    }

    private static AdminTokenMiddleware CreateMiddleware(Action<HttpContext> onNext, AdminAuthOptions? options = null)
    {
        return new AdminTokenMiddleware(
            context =>
            {
                onNext(context);
                return Task.CompletedTask;
            },
            Options.Create(options ?? new AdminAuthOptions { Token = "secret-token" }));
    }

    private static DefaultHttpContext CreateContext(string url)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            context.Request.Path = absolute.AbsolutePath;
            context.Request.QueryString = new QueryString(absolute.Query);
            return context;
        }

        var prefixed = url.StartsWith('/') ? $"http://localhost{url}" : $"http://localhost/{url}";
        var uri = new Uri(prefixed);
        context.Request.Path = uri.AbsolutePath;
        context.Request.QueryString = new QueryString(uri.Query);
        return context;
    }
}
