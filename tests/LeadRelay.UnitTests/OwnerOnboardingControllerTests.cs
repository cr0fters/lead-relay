using System.Net;
using System.Security.Cryptography;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerOnboardingControllerTests
{
    [Test]
    public async Task save_domain_preserves_existing_domains_and_redirects()
    {
        using var db = CreateDb();
        var site = CreateSite(["existing.example"]);
        db.Sites.Add(ToRecord(site));
        await db.SaveChangesAsync();
        var sites = new MutableSiteRepository(site);
        var controller = CreateController(db, sites);

        var result = await controller.SaveDomain("new.example", CancellationToken.None);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        Assert.That(sites.Site.AllowedDomains, Is.EquivalentTo(new[] { "existing.example", "new.example" }));
    }

    [Test]
    public async Task progress_only_counts_a_whatsapp_lead()
    {
        using var db = CreateDb();
        var site = CreateSite([]);
        db.Sites.Add(ToRecord(site));
        db.Leads.Add(new LeadRecord
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            CustomerId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Channel = "api",
            Status = "new",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new MutableSiteRepository(site));

        var apiOnlyResult = (ViewResult)await controller.Index(CancellationToken.None);
        Assert.That(((OwnerOnboardingController.OwnerOnboardingModel)apiOnlyResult.Model!).HasFirstLead, Is.False);

        var lead = await db.Leads.SingleAsync();
        lead.Channel = "whatsapp";
        await db.SaveChangesAsync();

        var whatsAppResult = (ViewResult)await controller.Index(CancellationToken.None);
        Assert.That(((OwnerOnboardingController.OwnerOnboardingModel)whatsAppResult.Model!).HasFirstLead, Is.True);
    }

    private static OwnerOnboardingController CreateController(LeadRelayDbContext db, MutableSiteRepository sites)
    {
        var settings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var protector = new WhatsAppCredentialProtector(settings);
        var http = new HttpClient(new SuccessHandler());
        var client = new WhatsAppClient(http, settings, sites, db, protector, NullLogger<WhatsAppClient>.Instance);
        var onboarding = new WhatsAppOnboardingService(
            http,
            db,
            sites,
            protector,
            client,
            new FixedClock(DateTimeOffset.UtcNow),
            settings,
            NullLogger<WhatsAppOnboardingService>.Instance);
        var controller = new OwnerOnboardingController(
            sites,
            db,
            onboarding,
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        var context = new DefaultHttpContext();
        context.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext(sites.Site.Id, sites.Site.OwnerEmail);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("leadrelay.test");
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new InMemoryTempDataProvider());
        return controller;
    }

    private static Site CreateSite(IReadOnlyList<string> domains) => new()
    {
        Id = "site_a",
        Name = "Site A",
        OwnerEmail = "owner@example.com",
        WhatsAppNumber = "",
        AllowedDomains = domains
    };

    private static SiteRecord ToRecord(Site site) => new()
    {
        Id = site.Id,
        Name = site.Name,
        OwnerEmail = site.OwnerEmail,
        WhatsAppNumber = site.WhatsAppNumber,
        AllowedDomains = site.AllowedDomains.ToList()
    };

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"owner-onboarding-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private sealed class MutableSiteRepository(Site site) : ISiteRepository
    {
        public Site Site { get; private set; } = site;
        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
            => Task.FromResult<Site?>(siteId == Site.Id ? Site : null);
        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult<Site?>(Site.WhatsAppPhoneNumberId == phoneNumberId ? Site : null);
        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Site>>([Site]);
        public Task UpsertAsync(Site updated, CancellationToken ct)
        {
            Site = updated;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = [];
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(_values);
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = new Dictionary<string, object>(values);
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
}
