using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Test]
    public async Task production_https_responses_receive_enforcing_security_headers()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new TestEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers["Content-Security-Policy"].ToString(), Does.Contain("frame-ancestors 'none'"));
            Assert.That(context.Response.Headers["Content-Security-Policy"].ToString(), Does.Contain("object-src 'none'"));
            Assert.That(context.Response.Headers["X-Content-Type-Options"].ToString(), Is.EqualTo("nosniff"));
            Assert.That(context.Response.Headers["X-Frame-Options"].ToString(), Is.EqualTo("DENY"));
            Assert.That(context.Response.Headers["Referrer-Policy"].ToString(), Is.EqualTo("strict-origin-when-cross-origin"));
            Assert.That(context.Response.Headers["Strict-Transport-Security"].ToString(), Is.EqualTo("max-age=31536000"));
            Assert.That(context.Response.Headers["Permissions-Policy"].ToString(), Is.EqualTo("camera=(), geolocation=(), microphone=()"));
        });
    }

    [Test]
    public async Task hsts_is_not_sent_for_non_https_requests()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new TestEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.That(context.Response.Headers["Strict-Transport-Security"], Is.Empty);
        Assert.That(context.Response.Headers["Content-Security-Policy"], Is.Not.Empty);
    }

    [Test]
    public async Task headers_are_applied_after_downstream_response_headers_are_cleared()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(
            async currentContext =>
            {
                currentContext.Response.Headers.Clear();
                await currentContext.Response.StartAsync();
            },
            new TestEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.Headers["Content-Security-Policy"], Is.Not.Empty);
        Assert.That(context.Response.Headers["X-Frame-Options"].ToString(), Is.EqualTo("DENY"));
    }

    [Test]
    public async Task development_responses_are_left_unrestricted_for_local_diagnostics()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new TestEnvironment(Environments.Development));

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.That(context.Response.Headers["Content-Security-Policy"], Is.Empty);
        Assert.That(context.Response.Headers["Strict-Transport-Security"], Is.Empty);
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "LeadRelay.UnitTests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
