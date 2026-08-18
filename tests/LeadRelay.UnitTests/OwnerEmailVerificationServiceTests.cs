using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerEmailVerificationServiceTests
{
    [Test]
    public async Task request_stores_hashed_expiring_token_and_sends_link()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        var email = new RecordingEmailSender();
        var service = CreateService(db, new MutableClock(now), email);
        string? rawToken = null;

        var sent = await service.RequestAsync(
            "site_demo",
            token =>
            {
                rawToken = token;
                return $"https://leadrelay.test/owner/verify-email/confirm?token={token}";
            },
            CancellationToken.None);

        var account = await db.OwnerAccounts.SingleAsync();
        Assert.That(sent, Is.True);
        Assert.That(email.Calls, Has.Count.EqualTo(1));
        Assert.That(email.Calls[0].Body, Does.Contain("https://leadrelay.test/owner/verify-email/confirm?token="));
        Assert.That(account.EmailVerificationTokenHash, Is.Not.Null.And.Not.Empty);
        Assert.That(account.EmailVerificationTokenHash, Is.Not.EqualTo(rawToken));
        Assert.That(email.Calls[0].Body, Does.Not.Contain(account.EmailVerificationTokenHash!));
        Assert.That(account.EmailVerificationTokenExpiresAtUtc, Is.EqualTo(now.AddHours(24)));
        Assert.That(account.EmailVerificationSentAtUtc, Is.EqualTo(now));
    }

    [Test]
    public async Task token_is_single_use_and_records_verification_time()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableClock(now);
        var email = new RecordingEmailSender();
        var service = CreateService(db, clock, email);
        string? token = null;
        await service.RequestAsync("site_demo", value => { token = value; return $"https://leadrelay.test/{value}"; }, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(5));
        var first = await service.VerifyAsync("OWNER@example.com", token, CancellationToken.None);
        var second = await service.VerifyAsync("owner@example.com", token, CancellationToken.None);
        var account = await db.OwnerAccounts.SingleAsync();

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
        Assert.That(account.EmailVerifiedAtUtc, Is.EqualTo(clock.UtcNow));
        Assert.That(account.EmailVerificationTokenHash, Is.Null);
        Assert.That(account.EmailVerificationTokenExpiresAtUtc, Is.Null);
    }

    [Test]
    public async Task expired_token_is_rejected()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var clock = new MutableClock(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(db, clock, new RecordingEmailSender());
        string? token = null;
        await service.RequestAsync("site_demo", value => { token = value; return $"https://leadrelay.test/{value}"; }, CancellationToken.None);

        clock.Advance(TimeSpan.FromHours(25));

        Assert.That(await service.VerifyAsync("owner@example.com", token, CancellationToken.None), Is.False);
        Assert.That((await db.OwnerAccounts.SingleAsync()).EmailVerifiedAtUtc, Is.Null);
    }

    [Test]
    public async Task token_for_one_email_cannot_verify_another_account()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var service = CreateService(db, new MutableClock(DateTimeOffset.UtcNow), new RecordingEmailSender());
        string? token = null;
        await service.RequestAsync("site_demo", value => { token = value; return $"https://leadrelay.test/{value}"; }, CancellationToken.None);

        Assert.That(await service.VerifyAsync("other@example.com", token, CancellationToken.None), Is.False);
        Assert.That((await db.OwnerAccounts.SingleAsync()).EmailVerifiedAtUtc, Is.Null);
    }

    [Test]
    public async Task resend_cooldown_suppresses_duplicate_delivery()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var email = new RecordingEmailSender();
        var service = CreateService(db, new MutableClock(DateTimeOffset.UtcNow), email);

        Assert.That(await service.RequestAsync("site_demo", _ => "https://leadrelay.test/one", CancellationToken.None), Is.True);
        Assert.That(await service.RequestAsync("site_demo", _ => "https://leadrelay.test/two", CancellationToken.None), Is.False);
        Assert.That(email.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task failed_delivery_does_not_start_resend_cooldown()
    {
        using var db = CreateDb();
        await SeedAccount(db);
        var email = new RecordingEmailSender { Fail = true };
        var service = CreateService(db, new MutableClock(DateTimeOffset.UtcNow), email);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RequestAsync("site_demo", _ => "https://leadrelay.test/one", CancellationToken.None));
        email.Fail = false;

        Assert.That(await service.RequestAsync("site_demo", _ => "https://leadrelay.test/two", CancellationToken.None), Is.True);
        Assert.That(email.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task legacy_site_without_owner_account_remains_publishable()
    {
        using var db = CreateDb();
        db.Sites.Add(CreateSite());
        await db.SaveChangesAsync();
        var service = CreateService(db, new MutableClock(DateTimeOffset.UtcNow), new RecordingEmailSender());

        Assert.That(await service.IsVerifiedAsync("site_demo", CancellationToken.None), Is.True);
    }

    [Test]
    public async Task passwordless_legacy_placeholder_remains_publishable()
    {
        using var db = CreateDb();
        db.Sites.Add(CreateSite());
        db.OwnerAccounts.Add(new OwnerAccountRecord
        {
            SiteId = "site_demo",
            PasswordHash = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, new MutableClock(DateTimeOffset.UtcNow), new RecordingEmailSender());

        Assert.That(await service.IsVerifiedAsync("site_demo", CancellationToken.None), Is.True);
    }

    private static OwnerEmailVerificationService CreateService(LeadRelayDbContext db, IClock clock, IEmailSender email)
        => new(db, clock, email, Options.Create(new OwnerPortalOptions
        {
            EmailVerificationTtlHours = 24,
            EmailVerificationResendCooldownSeconds = 60
        }));

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"owner-email-verification-tests-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private static async Task SeedAccount(LeadRelayDbContext db)
    {
        db.Sites.Add(CreateSite());
        db.OwnerAccounts.Add(new OwnerAccountRecord
        {
            SiteId = "site_demo",
            PasswordHash = "password-hash",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static SiteRecord CreateSite() => new()
    {
        Id = "site_demo",
        Name = "Demo Site",
        OwnerEmail = "owner@example.com",
        WhatsAppNumber = "447000000000"
    };

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Calls { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("Simulated email provider failure.");
            Calls.Add((toEmail, subject, bodyText));
            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
        public void Advance(TimeSpan value) => UtcNow = UtcNow.Add(value);
    }
}
