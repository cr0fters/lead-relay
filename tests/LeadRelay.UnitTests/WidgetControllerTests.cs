using System.Net;
using System.Security.Cryptography;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using LeadRelay.Web.Widgets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WidgetControllerTests
{
    [Test]
    public async Task successful_bootstrap_from_allowed_website_records_installation()
    {
        await using var db = CreateDb();
        var site = new Site
        {
            Id = "site_demo",
            Name = "Demo",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            AllowedDomains = ["customer.example"]
        };
        db.Sites.Add(new SiteRecord
        {
            Id = site.Id,
            Name = site.Name,
            OwnerEmail = site.OwnerEmail,
            WhatsAppNumber = site.WhatsAppNumber,
            AllowedDomains = site.AllowedDomains.ToList()
        });
        db.OwnerAccounts.Add(new OwnerAccountRecord { SiteId = site.Id });
        await db.SaveChangesAsync();
        var tracker = new RecordingInstallationTracker();
        var controller = new WidgetController(
            new FixedSiteRepository(site),
            CreateOnboarding(db, new FixedSiteRepository(site)),
            new FixedEmailVerificationService(true),
            tracker,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PublicBaseUrl"] = "https://leadrelay.test"
            }).Build(),
            NullLogger<WidgetController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("leadrelay.test");
        controller.Request.Headers.Referer = "https://customer.example/contact";

        var result = await controller.Bootstrap(site.Id, CancellationToken.None);

        Assert.That(((ContentResult)result).Content, Does.Contain("__LeadRelayWidgetConfig"));
        Assert.That(tracker.SiteId, Is.EqualTo(site.Id));
    }

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
            null!,
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

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"widget-controller-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private static WhatsAppOnboardingService CreateOnboarding(LeadRelayDbContext db, ISiteRepository sites)
    {
        var options = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var protector = new WhatsAppCredentialProtector(options);
        var http = new HttpClient(new SuccessHandler());
        var client = new WhatsAppClient(http, options, sites, db, protector, NullLogger<WhatsAppClient>.Instance);
        return new WhatsAppOnboardingService(
            http,
            db,
            sites,
            protector,
            client,
            new FixedClock(DateTimeOffset.UtcNow),
            options,
            NullLogger<WhatsAppOnboardingService>.Instance);
    }

    private sealed class RecordingInstallationTracker : IWidgetInstallationTracker
    {
        public string? SiteId { get; private set; }
        public Task RecordSuccessfulLoadAsync(string siteId, string domain, CancellationToken ct)
        {
            SiteId = siteId;
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
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
