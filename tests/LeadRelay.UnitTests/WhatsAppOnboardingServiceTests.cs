using System.Net;
using System.Security.Cryptography;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppOnboardingServiceTests
{
    [Test]
    public async Task embedded_signup_exchanges_code_discovers_coexistence_phone_and_stores_encrypted_token()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = ""
        });
        await db.SaveChangesAsync();
        var site = new Site { Id = "site_a", Name = "Site A", OwnerEmail = "owner@example.com", WhatsAppNumber = "" };
        var sites = new MutableSiteRepository(site);
        var settings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            EmbeddedSignupEnabled = true,
            MetaAppId = "123456",
            EmbeddedSignupConfigurationId = "987654",
            EmbeddedSignupVersion = "v4",
            AppSecret = "app-secret"
        });
        var protector = new WhatsAppCredentialProtector(settings);
        var handler = new EmbeddedSignupHandler();
        var client = new WhatsAppClient(
            new HttpClient(new SuccessHandler()), settings, sites, db, protector,
            NullLogger<WhatsAppClient>.Instance);
        var now = new DateTimeOffset(2026, 8, 19, 15, 0, 0, TimeSpan.Zero);
        var service = new WhatsAppOnboardingService(
            new HttpClient(handler), db, sites, protector, client, new FixedClock(now), settings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var result = await service.ConnectEmbeddedSignupAsync(
            "site_a",
            new WhatsAppEmbeddedSignupRequest("one-time-code", "555555", null),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True, result.Error);
        var connection = await db.WhatsAppConnections.AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(connection.WabaId, Is.EqualTo("555555"));
            Assert.That(connection.PhoneNumberId, Is.EqualTo("777777"));
            Assert.That(connection.DisplayPhoneNumber, Is.EqualTo("447000000000"));
            Assert.That(connection.WebhookSubscribedAtUtc, Is.EqualTo(now));
            Assert.That(connection.AccessTokenCiphertext, Does.Not.Contain("embedded-token"));
            Assert.That(sites.Site.WhatsAppPhoneNumberId, Is.EqualTo("777777"));
            Assert.That(handler.Requests.Count, Is.EqualTo(4));
            Assert.That(handler.Requests[0].RequestUri!.AbsolutePath, Does.EndWith("/oauth/access_token"));
            Assert.That(handler.Requests[0].RequestUri!.Query, Does.Contain("client_id=123456"));
            Assert.That(handler.Requests[0].Headers.Authorization, Is.Null);
            Assert.That(handler.Requests.Skip(1).All(x => x.Headers.Authorization?.Parameter == "embedded-token"), Is.True);
            Assert.That(handler.Requests[1].RequestUri!.AbsolutePath, Does.EndWith("/555555/phone_numbers"));
            Assert.That(handler.Requests[2].RequestUri!.AbsolutePath, Does.EndWith("/777777"));
            Assert.That(handler.Requests[3].RequestUri!.AbsolutePath, Does.EndWith("/555555/subscribed_apps"));
            Assert.That(handler.Requests.Any(x => x.RequestUri!.AbsolutePath.EndsWith("/register", StringComparison.Ordinal)), Is.False);
        });
        Assert.That(protector.TryUnprotect("site_a", connection.AccessTokenCiphertext, out var token), Is.True);
        Assert.That(token, Is.EqualTo("embedded-token"));
    }

    [Test]
    public async Task embedded_signup_rejects_a_number_that_is_not_on_the_business_app()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" });
        await db.SaveChangesAsync();
        var site = new Site { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" };
        var sites = new MutableSiteRepository(site);
        var settings = CreateEmbeddedSignupSettings();
        var protector = new WhatsAppCredentialProtector(settings);
        var handler = new EmbeddedSignupHandler(isOnBusinessApp: false);
        var client = new WhatsAppClient(
            new HttpClient(new SuccessHandler()), settings, sites, db, protector,
            NullLogger<WhatsAppClient>.Instance);
        var service = new WhatsAppOnboardingService(
            new HttpClient(handler), db, sites, protector, client, new FixedClock(DateTimeOffset.UtcNow), settings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var result = await service.ConnectEmbeddedSignupAsync(
            "site_a",
            new WhatsAppEmbeddedSignupRequest("one-time-code", "555555", "777777"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("not eligible"));
        Assert.That(await db.WhatsAppConnections.AnyAsync(), Is.False);
        Assert.That(handler.Requests.Count, Is.EqualTo(3), "The WABA must not be subscribed after coexistence validation fails.");
    }

    [Test]
    public async Task embedded_signup_cannot_claim_a_phone_connected_to_another_tenant()
    {
        using var db = CreateDb();
        db.Sites.AddRange(
            new SiteRecord { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" },
            new SiteRecord { Id = "site_b", Name = "B", OwnerEmail = "b@example.com", WhatsAppNumber = "447000000000" });
        db.WhatsAppConnections.Add(new WhatsAppConnectionRecord
        {
            SiteId = "site_b",
            WabaId = "444444",
            PhoneNumberId = "777777",
            DisplayPhoneNumber = "447000000000",
            AccessTokenCiphertext = "ciphertext",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var site = new Site { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" };
        var sites = new MutableSiteRepository(site);
        var settings = CreateEmbeddedSignupSettings();
        var protector = new WhatsAppCredentialProtector(settings);
        var handler = new EmbeddedSignupHandler();
        var client = new WhatsAppClient(
            new HttpClient(new SuccessHandler()), settings, sites, db, protector,
            NullLogger<WhatsAppClient>.Instance);
        var service = new WhatsAppOnboardingService(
            new HttpClient(handler), db, sites, protector, client, new FixedClock(DateTimeOffset.UtcNow), settings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var result = await service.ConnectEmbeddedSignupAsync(
            "site_a",
            new WhatsAppEmbeddedSignupRequest("one-time-code", "555555", "777777"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("another account"));
        Assert.That(handler.Requests.Count, Is.EqualTo(3), "A conflicting phone must not be subscribed to the current tenant.");
        Assert.That(await db.WhatsAppConnections.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task connect_validates_subscribes_and_stores_encrypted_credentials()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = ""
        });
        await db.SaveChangesAsync();

        var site = new Site
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = ""
        };
        var sites = new MutableSiteRepository(site);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var settings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = key
        });
        var protector = new WhatsAppCredentialProtector(settings);
        var graphHandler = new GraphHandler();
        var sendHandler = new SuccessHandler();
        var whatsAppClient = new WhatsAppClient(
            new HttpClient(sendHandler),
            settings,
            sites,
            db,
            protector,
            NullLogger<WhatsAppClient>.Instance);
        var now = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
        var service = new WhatsAppOnboardingService(
            new HttpClient(graphHandler),
            db,
            sites,
            protector,
            whatsAppClient,
            new FixedClock(now),
            settings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var connected = await service.ConnectAsync(
            "site_a",
            new WhatsAppConnectRequest("987654", "123456", null, "secret-token"),
            CancellationToken.None);

        Assert.That(connected.Succeeded, Is.True, connected.Error);
        var record = await db.WhatsAppConnections.AsNoTracking().SingleAsync();
        Assert.That(record.AccessTokenCiphertext, Does.Not.Contain("secret-token"));
        Assert.That(protector.TryUnprotect("site_a", record.AccessTokenCiphertext, out var token), Is.True);
        Assert.That(token, Is.EqualTo("secret-token"));
        Assert.That(record.Status, Is.EqualTo(WhatsAppConnectionStatuses.Connected));
        Assert.That(record.WebhookSubscribedAtUtc, Is.EqualTo(now));
        Assert.That(sites.Site.WhatsAppPhoneNumberId, Is.EqualTo("123456"));
        Assert.That(sites.Site.WhatsAppNumber, Is.EqualTo("447000000000"));
        Assert.That(graphHandler.Requests.Count, Is.EqualTo(2));
        Assert.That(graphHandler.Requests.All(x => x.RequestUri!.AbsolutePath.StartsWith("/v23.0/", StringComparison.Ordinal)), Is.True);
        var summary = await service.GetSummaryAsync("site_a", CancellationToken.None);
        Assert.That(summary.IsWebhookSubscribed, Is.True);
        Assert.That(summary.IsWebhookVerified, Is.False);

        var tested = await service.SendTestAsync("site_a", "+44 7111 111111", CancellationToken.None);

        Assert.That(tested.Succeeded, Is.True, tested.Error);
        record = await db.WhatsAppConnections.AsNoTracking().SingleAsync();
        Assert.That(record.LastOutboundTestAtUtc, Is.EqualTo(now));
        Assert.That(record.LastOutboundTestRecipient, Is.EqualTo("447111111111"));
        Assert.That(sendHandler.AuthorizationParameter, Is.EqualTo("secret-token"));

        var isTest = await service.RecordInboundAsync("site_a", "447111111111", CancellationToken.None);

        Assert.That(isTest, Is.True);
        summary = await service.GetSummaryAsync("site_a", CancellationToken.None);
        Assert.That(summary.IsWebhookVerified, Is.True);
        record = await db.WhatsAppConnections.AsNoTracking().SingleAsync();
        Assert.That(record.LastOutboundTestRecipient, Is.EqualTo("447111111111"));

        var laterInboundIsTest = await service.RecordInboundAsync("site_a", "447111111111", CancellationToken.None);
        Assert.That(laterInboundIsTest, Is.True,
            "The configured setup recipient remains test-only so retries and later test conversations cannot become real leads.");
    }

    [Test]
    public async Task connect_rejects_phone_number_already_owned_by_another_site()
    {
        using var db = CreateDb();
        db.Sites.AddRange(
            new SiteRecord { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" },
            new SiteRecord { Id = "site_b", Name = "B", OwnerEmail = "b@example.com", WhatsAppNumber = "" });
        db.WhatsAppConnections.Add(new WhatsAppConnectionRecord
        {
            SiteId = "site_b",
            WabaId = "222",
            PhoneNumberId = "123456",
            DisplayPhoneNumber = "447000000000",
            AccessTokenCiphertext = "ciphertext",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var site = new Site { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "" };
        var sites = new MutableSiteRepository(site);
        var settings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var protector = new WhatsAppCredentialProtector(settings);
        var client = new WhatsAppClient(new HttpClient(new SuccessHandler()), settings, sites, db, protector, NullLogger<WhatsAppClient>.Instance);
        var service = new WhatsAppOnboardingService(
            new HttpClient(new GraphHandler()), db, sites, protector, client, new FixedClock(DateTimeOffset.UtcNow),
            settings, NullLogger<WhatsAppOnboardingService>.Instance);

        var result = await service.ConnectAsync(
            "site_a",
            new WhatsAppConnectRequest("987654", "123456", null, "token"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("another account"));
    }

    [Test]
    public async Task unreadable_credential_is_reported_as_action_required()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "A",
            OwnerEmail = "a@example.com",
            WhatsAppNumber = "447000000000"
        });
        var originalSettings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var originalProtector = new WhatsAppCredentialProtector(originalSettings);
        db.WhatsAppConnections.Add(new WhatsAppConnectionRecord
        {
            SiteId = "site_a",
            WabaId = "987654",
            PhoneNumberId = "123456",
            DisplayPhoneNumber = "447000000000",
            AccessTokenCiphertext = originalProtector.Protect("site_a", "secret-token"),
            Status = WhatsAppConnectionStatuses.Connected,
            LastInboundAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var site = new Site { Id = "site_a", Name = "A", OwnerEmail = "a@example.com", WhatsAppNumber = "447000000000" };
        var sites = new MutableSiteRepository(site);
        var replacementSettings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var replacementProtector = new WhatsAppCredentialProtector(replacementSettings);
        var client = new WhatsAppClient(
            new HttpClient(new SuccessHandler()), replacementSettings, sites, db, replacementProtector,
            NullLogger<WhatsAppClient>.Instance);
        var service = new WhatsAppOnboardingService(
            new HttpClient(new SuccessHandler()), db, sites, replacementProtector, client,
            new FixedClock(DateTimeOffset.UtcNow), replacementSettings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var summary = await service.GetSummaryAsync("site_a", CancellationToken.None);

        Assert.That(summary.IsConnected, Is.False);
        Assert.That(summary.Status, Is.EqualTo(WhatsAppConnectionStatuses.ActionRequired));
        Assert.That(summary.LastError, Does.Contain("cannot be decrypted"));
    }

    [Test]
    public async Task reconnect_resets_test_and_resets_inbound_verification_for_a_new_sender()
    {
        using var db = CreateDb();
        var previous = new WhatsAppConnectionRecord
        {
            SiteId = "site_a",
            WabaId = "111111",
            PhoneNumberId = "222222",
            DisplayPhoneNumber = "447000000001",
            AccessTokenCiphertext = "old-ciphertext",
            Status = WhatsAppConnectionStatuses.ActionRequired,
            LastInboundAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastOutboundTestAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastOutboundTestRecipient = "447000000999",
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "A",
            OwnerEmail = "a@example.com",
            WhatsAppNumber = previous.DisplayPhoneNumber,
            WhatsAppPhoneNumberId = previous.PhoneNumberId
        });
        db.WhatsAppConnections.Add(previous);
        await db.SaveChangesAsync();

        var site = new Site
        {
            Id = "site_a",
            Name = "A",
            OwnerEmail = "a@example.com",
            WhatsAppNumber = previous.DisplayPhoneNumber,
            WhatsAppPhoneNumberId = previous.PhoneNumberId
        };
        var sites = new MutableSiteRepository(site);
        var settings = Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        var protector = new WhatsAppCredentialProtector(settings);
        var client = new WhatsAppClient(
            new HttpClient(new SuccessHandler()), settings, sites, db, protector,
            NullLogger<WhatsAppClient>.Instance);
        var service = new WhatsAppOnboardingService(
            new HttpClient(new GraphHandler("333333", "+44 7000 000002")),
            db,
            sites,
            protector,
            client,
            new FixedClock(DateTimeOffset.UtcNow),
            settings,
            NullLogger<WhatsAppOnboardingService>.Instance);

        var result = await service.ConnectAsync(
            "site_a",
            new WhatsAppConnectRequest("444444", "333333", null, "new-token"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True, result.Error);
        var updated = await db.WhatsAppConnections.AsNoTracking().SingleAsync();
        Assert.That(updated.LastInboundAtUtc, Is.Null);
        Assert.That(updated.LastOutboundTestAtUtc, Is.Null);
        Assert.That(updated.LastOutboundTestRecipient, Is.Null);
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"whatsapp-onboarding-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private static IOptions<WhatsAppOptions> CreateEmbeddedSignupSettings()
        => Options.Create(new WhatsAppOptions
        {
            CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            EmbeddedSignupEnabled = true,
            MetaAppId = "123456",
            EmbeddedSignupConfigurationId = "987654",
            EmbeddedSignupVersion = "v4",
            AppSecret = "app-secret"
        });

    private sealed class EmbeddedSignupHandler(bool isOnBusinessApp = true) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/oauth/access_token", StringComparison.Ordinal)
                ? "{\"access_token\":\"embedded-token\",\"token_type\":\"bearer\"}"
                : path.EndsWith("/phone_numbers", StringComparison.Ordinal)
                    ? "{\"data\":[{\"id\":\"777777\"}]}"
                    : path.EndsWith("/777777", StringComparison.Ordinal)
                        ? $"{{\"id\":\"777777\",\"display_phone_number\":\"+44 7000 000000\",\"is_on_biz_app\":{isOnBusinessApp.ToString().ToLowerInvariant()}}}"
                        : "{\"success\":true}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class GraphHandler(
        string returnedPhoneNumberId = "123456",
        string displayPhoneNumber = "+44 7000 000000") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Method == HttpMethod.Get
                ? $"{{\"id\":\"{returnedPhoneNumberId}\",\"display_phone_number\":\"{displayPhoneNumber}\",\"verified_name\":\"Site A\"}}"
                : "{\"success\":true}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class MutableSiteRepository(Site site) : ISiteRepository
    {
        public Site Site { get; private set; } = site;
        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct) => Task.FromResult<Site?>(Site.Id == siteId ? Site : null);
        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult<Site?>(Site.WhatsAppPhoneNumberId == phoneNumberId ? Site : null);
        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Site>>([Site]);
        public Task UpsertAsync(Site updated, CancellationToken ct)
        {
            Site = updated;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
