using LeadRelay.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Reflection;

namespace LeadRelay.UnitTests;

public sealed class VersionControllerTests
{
    [Test]
    public void endpoint_returns_validated_railway_commit_and_disables_indexing_and_caching()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RAILWAY_GIT_COMMIT_SHA"] = "ABCDEF0123456789ABCDEF0123456789ABCDEF01"
            })
            .Build();
        var controller = CreateController(configuration);

        var result = controller.Get();

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var response = ((OkObjectResult)result).Value as VersionController.VersionResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Service, Is.EqualTo("leadrelay"));
        Assert.That(response.CommitSha, Is.EqualTo("abcdef0123456789abcdef0123456789abcdef01"));
        Assert.That(response.Version, Is.Not.Empty);
        Assert.That(controller.Response.Headers["X-Robots-Tag"].ToString(), Is.EqualTo("noindex, nofollow, noarchive"));
        var cachePolicy = typeof(VersionController).GetMethod(nameof(VersionController.Get))!
            .GetCustomAttribute<ResponseCacheAttribute>();
        Assert.That(cachePolicy?.NoStore, Is.True);
        Assert.That(cachePolicy?.Location, Is.EqualTo(ResponseCacheLocation.None));
    }

    [Test]
    public void endpoint_never_echoes_an_invalid_commit_value()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RAILWAY_GIT_COMMIT_SHA"] = "not-a-sha-or-a-secret"
            })
            .Build();
        var controller = CreateController(configuration);

        var result = (OkObjectResult)controller.Get();
        var response = (VersionController.VersionResponse)result.Value!;

        Assert.That(response.CommitSha, Is.Not.EqualTo("not-a-sha-or-a-secret"));
        if (response.CommitSha is not null)
            Assert.That(response.CommitSha, Does.Match("^[0-9a-f]{7,64}$"));
    }

    private static VersionController CreateController(IConfiguration configuration)
    {
        return new VersionController(configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
