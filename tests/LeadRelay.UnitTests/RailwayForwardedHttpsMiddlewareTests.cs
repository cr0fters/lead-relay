using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class RailwayForwardedHttpsMiddlewareTests
{
    [Test]
    public async Task railway_https_forwarding_upgrades_request_scheme_before_downstream_middleware()
    {
        var downstreamSawHttps = false;
        var middleware = CreateMiddleware(
            context => downstreamSawHttps = context.Request.IsHttps,
            railwayEnvironment: "production");
        var context = CreateHttpContext("https");

        await middleware.InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Request.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
            Assert.That(downstreamSawHttps, Is.True);
        });
    }

    [Test]
    public async Task railway_https_forwarding_allows_secure_antiforgery_token_generation()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddDataProtection();
        serviceCollection.AddAntiforgery(options =>
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
        using var services = serviceCollection.BuildServiceProvider();
        var antiforgery = services.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        var tokenGenerated = false;
        var middleware = CreateMiddleware(
            context =>
            {
                antiforgery.GetAndStoreTokens(context);
                tokenGenerated = true;
            },
            railwayEnvironment: "production");
        var context = CreateHttpContext("https");
        context.RequestServices = services;

        await middleware.InvokeAsync(context);

        Assert.That(tokenGenerated, Is.True);
    }

    [TestCase("http")]
    [TestCase("https,http")]
    [TestCase("")]
    public async Task railway_non_https_or_ambiguous_forwarding_is_not_trusted(string forwardedProto)
    {
        var middleware = CreateMiddleware(_ => { }, railwayEnvironment: "production");
        var context = CreateHttpContext(forwardedProto);

        await middleware.InvokeAsync(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(Uri.UriSchemeHttp));
    }

    [Test]
    public async Task forwarded_proto_is_ignored_outside_railway()
    {
        var middleware = CreateMiddleware(_ => { }, railwayEnvironment: null);
        var context = CreateHttpContext("https");

        await middleware.InvokeAsync(context);

        Assert.That(context.Request.Scheme, Is.EqualTo(Uri.UriSchemeHttp));
    }

    private static RailwayForwardedHttpsMiddleware CreateMiddleware(
        Action<HttpContext> onNext,
        string? railwayEnvironment)
    {
        var values = new Dictionary<string, string?>();
        if (railwayEnvironment is not null)
            values["RAILWAY_ENVIRONMENT"] = railwayEnvironment;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new RailwayForwardedHttpsMiddleware(
            context =>
            {
                onNext(context);
                return Task.CompletedTask;
            },
            configuration);
    }

    private static DefaultHttpContext CreateHttpContext(string forwardedProto)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        if (!string.IsNullOrEmpty(forwardedProto))
            context.Request.Headers[RailwayForwardedHttpsMiddleware.ForwardedProtoHeader] = forwardedProto;

        return context;
    }
}
