using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class EfSiteRepositoryWhatsAppConnectionTests
{
    [Test]
    public async Task self_serve_connection_is_sender_identity_source_of_truth()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"site-whatsapp-source-{Guid.NewGuid():N}")
            .Options;
        await using var db = new LeadRelayDbContext(options);
        db.Sites.Add(new SiteRecord
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "legacy-number",
            WhatsAppPhoneNumberId = "legacy-id"
        });
        db.WhatsAppConnections.Add(new WhatsAppConnectionRecord
        {
            SiteId = "site_a",
            WabaId = "987654",
            PhoneNumberId = "123456",
            DisplayPhoneNumber = "447000000000",
            AccessTokenCiphertext = "ciphertext",
            Status = "connected",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var repository = new EfSiteRepository(db);

        var byId = await repository.GetByIdAsync("site_a", CancellationToken.None);
        var byPhoneId = await repository.GetByWhatsAppPhoneNumberIdAsync("123456", CancellationToken.None);

        Assert.That(byId, Is.Not.Null);
        Assert.That(byId!.WhatsAppNumber, Is.EqualTo("447000000000"));
        Assert.That(byId.WhatsAppPhoneNumberId, Is.EqualTo("123456"));
        Assert.That(byPhoneId?.Id, Is.EqualTo("site_a"));
    }
}
