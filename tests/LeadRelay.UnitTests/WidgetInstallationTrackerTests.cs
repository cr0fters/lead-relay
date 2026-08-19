using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Widgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WidgetInstallationTrackerTests
{
    [Test]
    public async Task successful_load_records_the_first_observed_time_only()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"widget-installation-{Guid.NewGuid():N}")
            .Options;
        await using var db = new LeadRelayDbContext(options);
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        });
        db.OwnerAccounts.Add(new OwnerAccountRecord { SiteId = "site_a" });
        await db.SaveChangesAsync();
        var firstLoad = new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.Zero);

        var tracker = new WidgetInstallationTracker(
            db,
            new FixedClock(firstLoad),
            NullLogger<WidgetInstallationTracker>.Instance);
        await tracker.RecordSuccessfulLoadAsync("site_a", "www.example.com", CancellationToken.None);
        await new WidgetInstallationTracker(
                db,
                new FixedClock(firstLoad.AddHours(1)),
                NullLogger<WidgetInstallationTracker>.Instance)
            .RecordSuccessfulLoadAsync("site_a", "www.example.com", CancellationToken.None);

        var account = await db.OwnerAccounts.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(account.WidgetInstalledAtUtc, Is.EqualTo(firstLoad));
            Assert.That(account.WidgetInstalledDomain, Is.EqualTo("www.example.com"));
        });
    }

    [Test]
    public async Task successful_load_for_unknown_site_is_ignored()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"widget-installation-{Guid.NewGuid():N}")
            .Options;
        await using var db = new LeadRelayDbContext(options);
        var tracker = new WidgetInstallationTracker(
            db,
            new FixedClock(DateTimeOffset.UtcNow),
            NullLogger<WidgetInstallationTracker>.Instance);

        Assert.DoesNotThrowAsync(() => tracker.RecordSuccessfulLoadAsync("missing", "www.example.com", CancellationToken.None));
    }

    private sealed class FixedClock(DateTimeOffset now) : LeadRelay.Application.Abstractions.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
