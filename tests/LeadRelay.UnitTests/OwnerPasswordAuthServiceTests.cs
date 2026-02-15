using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Email;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerPasswordAuthServiceTests
{
    [Test]
    public async Task request_password_reset_uses_postmark_template_when_configured()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_demo",
            Name = "Demo Site",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        });
        await db.SaveChangesAsync();

        var email = new RecordingEmailSender();
        var service = new OwnerPasswordAuthService(
            db,
            new FixedClock(new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero)),
            email,
            Options.Create(new OwnerPortalOptions
            {
                SigningSecret = "secret",
                PasswordResetTtlMinutes = 30
            }),
            Options.Create(new PostmarkOptions
            {
                Enabled = true,
                PasswordResetTemplateAlias = "password-reset",
                PasswordResetTemplateId = 43533665
            }));

        await service.RequestPasswordResetAsync(
            "owner@example.com",
            token => $"https://leadrelay.test/owner/password/reset?email=owner%40example.com&token={token}",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Version/17.4 Safari/605.1.15",
            CancellationToken.None);

        Assert.That(email.TemplateCalls.Count, Is.EqualTo(1));
        var call = email.TemplateCalls[0];
        Assert.That(call.ToEmail, Is.EqualTo("owner@example.com"));
        Assert.That(call.TemplateAlias, Is.EqualTo("password-reset"));
        Assert.That(call.TemplateId, Is.EqualTo(43533665));
        Assert.That(call.TemplateModel["product_name"], Is.EqualTo("LeadRelay"));
        Assert.That(call.TemplateModel["browser_name"], Is.EqualTo("Safari"));
        Assert.That(call.TemplateModel["operating_system"], Is.EqualTo("macOS"));
        Assert.That(call.TemplateModel["action_url"], Does.StartWith("https://leadrelay.test/owner/password/reset"));
    }

    [Test]
    public async Task request_password_reset_falls_back_to_plain_email_when_template_not_configured()
    {
        using var db = CreateDb();
        db.Sites.Add(new SiteRecord
        {
            Id = "site_demo",
            Name = "Demo Site",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000"
        });
        await db.SaveChangesAsync();

        var email = new RecordingEmailSender();
        var service = new OwnerPasswordAuthService(
            db,
            new FixedClock(new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero)),
            email,
            Options.Create(new OwnerPortalOptions
            {
                SigningSecret = "secret",
                PasswordResetTtlMinutes = 30
            }),
            Options.Create(new PostmarkOptions
            {
                Enabled = true
            }));

        await service.RequestPasswordResetAsync(
            "owner@example.com",
            token => $"https://leadrelay.test/owner/password/reset?email=owner%40example.com&token={token}",
            null,
            CancellationToken.None);

        Assert.That(email.TemplateCalls, Is.Empty);
        Assert.That(email.PlainCalls.Count, Is.EqualTo(1));
        Assert.That(email.PlainCalls[0].Subject, Is.EqualTo("Reset your LeadRelay owner password"));
    }

    private static LeadRelayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseInMemoryDatabase($"owner-password-auth-tests-{Guid.NewGuid():N}")
            .Options;
        return new LeadRelayDbContext(options);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string ToEmail, string Subject, string Body)> PlainCalls { get; } = [];
        public List<(string ToEmail, string? TemplateAlias, int? TemplateId, IReadOnlyDictionary<string, string> TemplateModel)> TemplateCalls { get; } = [];

        public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
        {
            PlainCalls.Add((toEmail, subject, bodyText));
            return Task.CompletedTask;
        }

        public Task SendTemplateAsync(string toEmail, string? templateAlias, int? templateId, IReadOnlyDictionary<string, string> templateModel, CancellationToken ct)
        {
            TemplateCalls.Add((toEmail, templateAlias, templateId, templateModel));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
