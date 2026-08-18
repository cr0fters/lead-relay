using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WidgetControllerTests
{
    [Test]
    public async Task bootstrap_does_not_publish_for_unverified_owner_account()
    {
        var site = new Site
        {
            Id = "site_demo",
            Name = "Demo",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        };
        var controller = new WidgetController(
            new FixedSiteRepository(site),
            null!,
            new FixedEmailVerificationService(false),
            new ConfigurationBuilder().Build(),
            NullLogger<WidgetController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Bootstrap(site.Id, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ContentResult>());
        Assert.That(((ContentResult)result).Content, Does.Contain("until the account email is verified"));
        Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
    }

    private sealed class FixedEmailVerificationService(bool verified) : IOwnerEmailVerificationService
    {
        public Task<bool> IsVerifiedAsync(string siteId, CancellationToken ct) => Task.FromResult(verified);
        public Task<bool> RequestAsync(string siteId, Func<string, string> verificationUrlFactory, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> VerifyAsync(string? email, string? token, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class FixedSiteRepository(Site site) : ISiteRepository
    {
        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
            => Task.FromResult<Site?>(string.Equals(site.Id, siteId, StringComparison.Ordinal) ? site : null);

        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult<Site?>(null);

        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Site>>([site]);

        public Task UpsertAsync(Site updatedSite, CancellationToken ct) => Task.CompletedTask;
    }
}
