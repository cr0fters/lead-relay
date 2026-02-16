using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerRegistrationServiceTests
{
    [Test]
    public async Task register_creates_site_and_owner_account()
    {
        using var db = CreateDb();
        var now = new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero);
        var service = new OwnerRegistrationService(db, new FixedClock(now));

        var result = await service.RegisterAsync(
            new OwnerRegistrationRequest(
                "Acme Interiors",
                "Interior design studio focused on family homes.",
                [
                    new ConversationField
                    {
                        Id = "timeline",
                        Name = "Timeline",
                        Description = "Target start date"
                    }
                ],
                "Owner@Example.com",
                "strong-pass"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Auth, Is.Not.Null);
        Assert.That(result.Auth!.OwnerEmail, Is.EqualTo("owner@example.com"));

        var site = await db.Sites.AsNoTracking().SingleAsync();
        Assert.That(site.Name, Is.EqualTo("Acme Interiors"));
        Assert.That(site.OwnerEmail, Is.EqualTo("owner@example.com"));
        Assert.That(site.BusinessSummary, Is.EqualTo("Interior design studio focused on family homes."));
        Assert.That(site.Fields.Count, Is.EqualTo(1));
        Assert.That(site.Fields[0].Id, Is.EqualTo("timeline"));
        Assert.That(site.WhatsAppNumber, Is.EqualTo(""));

        var account = await db.OwnerAccounts.AsNoTracking().SingleAsync();
        Assert.That(account.SiteId, Is.EqualTo(site.Id));
        Assert.That(account.UpdatedAtUtc, Is.EqualTo(now));
        Assert.That(account.PasswordHash, Is.Not.Null.And.Not.Empty);

        var hasher = new PasswordHasher<OwnerAccountRecord>();
        var verification = hasher.VerifyHashedPassword(account, account.PasswordHash!, "strong-pass");
        Assert.That(verification, Is.Not.EqualTo(PasswordVerificationResult.Failed));
    }

    [Test]
    public async Task register_generates_field_ids_from_names_when_missing()
    {
        using var db = CreateDb();
        var service = new OwnerRegistrationService(db, new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.RegisterAsync(
            new OwnerRegistrationRequest(
                "Acme Interiors",
                null,
                [
                    new ConversationField { Name = "Project scope", Description = "Describe your space" }
                ],
                "owner@example.com",
                "strong-pass"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var site = await db.Sites.AsNoTracking().SingleAsync();
        Assert.That(site.Fields.Count, Is.EqualTo(1));
        Assert.That(site.Fields[0].Id, Is.EqualTo("project_scope"));
    }

    [Test]
    public async Task register_rejects_duplicate_owner_email()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_existing",
            Name = "Existing",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        });
        await db.SaveChangesAsync();

        var service = new OwnerRegistrationService(db, new FixedClock(DateTimeOffset.UtcNow));
        var result = await service.RegisterAsync(
            new OwnerRegistrationRequest("New Site", null, [], "OWNER@example.com", "strong-pass"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("An account with that email already exists."));
        Assert.That(await db.Sites.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.OwnerAccounts.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task register_rejects_invalid_email()
    {
        using var db = CreateDb();
        var service = new OwnerRegistrationService(db, new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.RegisterAsync(
            new OwnerRegistrationRequest("New Site", null, [], "not-an-email", "strong-pass"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Enter a valid email address."));
        Assert.That(await db.Sites.CountAsync(), Is.EqualTo(0));
        Assert.That(await db.OwnerAccounts.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task register_rejects_duplicate_field_ids()
    {
        using var db = CreateDb();
        var service = new OwnerRegistrationService(db, new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.RegisterAsync(
            new OwnerRegistrationRequest(
                "Acme Interiors",
                null,
                [
                    new ConversationField { Id = "budget", Name = "Budget" },
                    new ConversationField { Id = "budget", Name = "Budget copy" }
                ],
                "owner@example.com",
                "strong-pass"),
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Field ids must be unique."));
        Assert.That(await db.Sites.CountAsync(), Is.EqualTo(0));
        Assert.That(await db.OwnerAccounts.CountAsync(), Is.EqualTo(0));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"owner-registration-tests-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
