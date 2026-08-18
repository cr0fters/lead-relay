using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Tokens;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerPortalAuthTests
{
    [Test]
    public async Task session_service_validates_site_owner_token()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var options = Options.Create(new OwnerPortalOptions { SigningSecret = "test-secret" });
        var service = new OwnerSessionService(options, new InMemorySiteRepository());
        var tokenService = new HmacTokenService("test-secret");

        var token = tokenService.CreateSignedToken(
            new Dictionary<string, string>
            {
                ["siteId"] = siteId,
                ["ownerEmail"] = "owner@example.com"
            },
            TimeSpan.FromMinutes(5));

        var auth = await service.ValidateAsync(token, CancellationToken.None);

        Assert.That(auth, Is.Not.Null);
        Assert.That(auth!.SiteId, Is.EqualTo(siteId));
        Assert.That(auth.OwnerEmail, Is.EqualTo("owner@example.com"));
    }

    [Test]
    public async Task session_service_rejects_wrong_owner_email_claim()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var options = Options.Create(new OwnerPortalOptions { SigningSecret = "test-secret" });
        var service = new OwnerSessionService(options, new InMemorySiteRepository());
        var tokenService = new HmacTokenService("test-secret");

        var token = tokenService.CreateSignedToken(
            new Dictionary<string, string>
            {
                ["siteId"] = siteId,
                ["ownerEmail"] = "intruder@example.com"
            },
            TimeSpan.FromMinutes(5));

        var auth = await service.ValidateAsync(token, CancellationToken.None);

        Assert.That(auth, Is.Null);
    }

    [Test]
    public async Task middleware_redirects_to_login_when_not_authenticated()
    {
        var middleware = new OwnerAuthMiddleware(_ => Task.CompletedTask);
        var context = CreateContext("/owner");
        var sessions = CreateSessionService("test-secret");

        await middleware.Invoke(context, sessions);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/owner/login?returnUrl=%2Fowner"));
    }

    [Test]
    public async Task middleware_allows_owner_route_with_valid_cookie_token()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var nextCalled = false;
        var middleware = new OwnerAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/owner");
        var sessions = CreateSessionService("test-secret");
        var tokenService = new HmacTokenService("test-secret");
        var token = tokenService.CreateSignedToken(
            new Dictionary<string, string>
            {
                ["siteId"] = siteId,
                ["ownerEmail"] = "owner@example.com"
            },
            TimeSpan.FromMinutes(5));

        context.Request.Headers.Cookie = $"leadrelay_owner_session={token}";

        await middleware.Invoke(context, sessions);

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Items.TryGetValue(OwnerAuthMiddleware.ContextKey, out var value), Is.True);
        Assert.That(value, Is.TypeOf<OwnerAuthContext>());
    }

    [Test]
    public async Task middleware_allows_login_route_without_authentication()
    {
        var nextCalled = false;
        var middleware = new OwnerAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/owner/login");
        var sessions = CreateSessionService("test-secret");

        await middleware.Invoke(context, sessions);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task middleware_allows_password_reset_routes_without_authentication()
    {
        var nextCalled = false;
        var middleware = new OwnerAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/owner/password/forgot");
        var sessions = CreateSessionService("test-secret");

        await middleware.Invoke(context, sessions);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task middleware_allows_registration_route_without_authentication()
    {
        var nextCalled = false;
        var middleware = new OwnerAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/owner/register");
        var sessions = CreateSessionService("test-secret");

        await middleware.Invoke(context, sessions);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task middleware_allows_email_confirmation_link_without_authentication()
    {
        var nextCalled = false;
        var middleware = new OwnerAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("/owner/verify-email/confirm?token=test");

        await middleware.Invoke(context, CreateSessionService("test-secret"));

        Assert.That(nextCalled, Is.True);
    }

    private static OwnerSessionService CreateSessionService(string secret)
    {
        return new OwnerSessionService(
            Options.Create(new OwnerPortalOptions
            {
                SigningSecret = secret,
                SessionCookieName = "leadrelay_owner_session",
                SessionTtlHours = 12,
                PasswordResetTtlMinutes = 30
            }),
            new InMemorySiteRepository());
    }

    private static DefaultHttpContext CreateContext(string url)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var uri = new Uri($"http://localhost{url}");
        context.Request.Path = uri.AbsolutePath;
        context.Request.QueryString = new QueryString(uri.Query);
        return context;
    }
}
