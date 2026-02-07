using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerPortalReplyChannelTests
{
    [Test]
    public async Task reply_uses_selected_email_channel()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = "site_demo",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Email = "lead@example.com",
            Phone = "447000000000",
            Notes = "channel=whatsapp"
        };

        var repository = new FakeLeadRepository(lead);
        var dispatcher = new RecordingDispatcher();
        var controller = new OwnerPortalController(repository, dispatcher)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext("site_demo", "owner@example.com");

        var result = await controller.Reply(lead.Id, "hello", "email", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(dispatcher.Channel, Is.EqualTo("email"));
        Assert.That(dispatcher.Recipient, Is.EqualTo("lead@example.com"));
    }

    [Test]
    public async Task update_contact_saves_email_and_phone()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = "site_demo",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Email = null,
            Phone = null
        };

        var repository = new FakeLeadRepository(lead);
        var controller = new OwnerPortalController(repository, new RecordingDispatcher())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext("site_demo", "owner@example.com");

        var result = await controller.UpdateContact(lead.Id, "Jane Owner", "jane@example.com", "+44 7000 000000", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.SavedLead, Is.Not.Null);
        Assert.That(repository.SavedLead!.Name, Is.EqualTo("Jane Owner"));
        Assert.That(repository.SavedLead.Email, Is.EqualTo("jane@example.com"));
        Assert.That(repository.SavedLead.Phone, Is.EqualTo("447000000000"));
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private readonly Lead _lead;
        public Lead? SavedLead { get; private set; }

        public FakeLeadRepository(Lead lead)
        {
            _lead = lead;
        }

        public Task SaveAsync(Lead lead, CancellationToken ct)
        {
            SavedLead = lead;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LeadSummary>> GetRecentAsync(int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LeadSummary>>(Array.Empty<LeadSummary>());

        public Task<IReadOnlyList<LeadSummary>> GetRecentBySiteAsync(string siteId, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LeadSummary>>(Array.Empty<LeadSummary>());

        public Task<LeadPageResult> SearchBySiteAsync(string siteId, string? query, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(new LeadPageResult(Array.Empty<LeadSummary>(), 0, 1, 20));

        public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Lead?>(_lead.Id == id ? _lead : null);

        public Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct)
            => Task.FromResult<Lead?>(_lead.Id == id && _lead.SiteId == siteId ? _lead : null);
    }

    private sealed class RecordingDispatcher : IMessageDispatcher
    {
        public string? Channel { get; private set; }
        public string? Recipient { get; private set; }

        public Task<MessageDispatchResult> SendTextAsync(string channel, string recipient, string text, CancellationToken ct)
        {
            Channel = channel;
            Recipient = recipient;
            return Task.FromResult(new MessageDispatchResult(true));
        }
    }
}
