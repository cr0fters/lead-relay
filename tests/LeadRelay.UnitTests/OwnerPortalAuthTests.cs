using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Tokens;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerPortalAuthTests
{
    [Test]
    public async Task session_service_validates_site_owner_token()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var service = CreateSessionService("test-secret");
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
        var service = CreateSessionService("test-secret");
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
    public async Task session_service_accepts_previous_signing_secret_during_rotation()
    {
        var service = CreateSessionService("new-secret", "old-secret");
        var token = new HmacTokenService("old-secret").CreateSignedToken(
            new Dictionary<string, string>
            {
                ["siteId"] = InMemorySiteRepository.DefaultSiteId,
                ["ownerEmail"] = "owner@example.com",
                ["sessionVersion"] = "1"
            },
            TimeSpan.FromMinutes(5));

        var auth = await service.ValidateAsync(token, CancellationToken.None);

        Assert.That(auth, Is.Not.Null);
    }

    [Test]
    public async Task session_service_rejects_revoked_session_version()
    {
        var service = CreateSessionService("test-secret", sessionVersion: 2);
        var token = new HmacTokenService("test-secret").CreateSignedToken(
            new Dictionary<string, string>
            {
                ["siteId"] = InMemorySiteRepository.DefaultSiteId,
                ["ownerEmail"] = "owner@example.com",
                ["sessionVersion"] = "1"
            },
            TimeSpan.FromMinutes(5));

        var auth = await service.ValidateAsync(token, CancellationToken.None);

        Assert.That(auth, Is.Null);
    }

    [Test]
    public void sign_in_forces_secure_cookie_in_production_even_for_http_request()
    {
        var service = CreateSessionService("test-secret", environmentName: Environments.Production);
        var context = CreateContext("/owner");

        service.SignIn(context, "signed-token");

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.That(cookie, Does.Contain("secure").IgnoreCase);
        Assert.That(cookie, Does.Contain("httponly").IgnoreCase);
        Assert.That(cookie, Does.Contain("samesite=lax").IgnoreCase);
        Assert.That(cookie, Does.Contain("path=/").IgnoreCase);
    }

    [Test]
    public void sign_in_allows_http_cookie_during_local_development()
    {
        var service = CreateSessionService("test-secret", environmentName: Environments.Development);
        var context = CreateContext("/owner");

        service.SignIn(context, "signed-token");

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.That(cookie.Contains("secure", StringComparison.OrdinalIgnoreCase), Is.False);
    }

    [Test]
    public void sign_out_deletes_cookie_with_matching_security_attributes()
    {
        var service = CreateSessionService("test-secret", environmentName: Environments.Production);
        var context = CreateContext("/owner");

        service.SignOut(context);

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.That(cookie, Does.Contain("expires=").IgnoreCase);
        Assert.That(cookie, Does.Contain("secure").IgnoreCase);
        Assert.That(cookie, Does.Contain("samesite=lax").IgnoreCase);
        Assert.That(cookie, Does.Contain("path=/").IgnoreCase);
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

    private static OwnerSessionService CreateSessionService(
        string secret,
        string? previousSecret = null,
        long sessionVersion = 1,
        string environmentName = Environments.Development)
    {
        return new OwnerSessionService(
            Options.Create(new OwnerPortalOptions
            {
                SigningSecret = secret,
                PreviousSigningSecret = previousSecret,
                SessionCookieName = "leadrelay_owner_session",
                SessionTtlHours = 12,
                PasswordResetTtlMinutes = 30
            }),
            new InMemorySiteRepository(),
            new FakeSessionVersionStore(sessionVersion),
            new TestWebHostEnvironment(environmentName));
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

    private sealed class FakeSessionVersionStore(long version) : IOwnerSessionVersionStore
    {
        public Task<long?> GetAsync(string siteId, CancellationToken ct) => Task.FromResult<long?>(version);
    }
}
