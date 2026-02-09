using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
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
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Email = "lead@example.com",
            Phone = "447000000000",
            Channel = "whatsapp",
            CustomerId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid()
        };

        var repository = new FakeLeadRepository(lead);
        var dispatcher = new RecordingDispatcher();
        var controller = new OwnerPortalController(repository, dispatcher, new LeadRelay.Infrastructure.Persistence.InMemorySiteRepository())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext(siteId, "owner@example.com");

        var result = await controller.Reply(lead.Id, "hello", "email", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(dispatcher.Channel, Is.EqualTo("email"));
        Assert.That(dispatcher.Recipient, Is.EqualTo("lead@example.com"));
    }

    [Test]
    public async Task update_contact_saves_email_and_phone()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Channel = "api",
            CustomerId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Email = null,
            Phone = null
        };

        var repository = new FakeLeadRepository(lead);
        var controller = new OwnerPortalController(repository, new RecordingDispatcher(), new LeadRelay.Infrastructure.Persistence.InMemorySiteRepository())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext(siteId, "owner@example.com");

        var result = await controller.UpdateContact(lead.Id, "Jane Owner", "jane@example.com", "+44 7000 000000", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.SavedLead, Is.Not.Null);
        Assert.That(repository.SavedLead!.Name, Is.EqualTo("Jane Owner"));
        Assert.That(repository.SavedLead.Email, Is.EqualTo("jane@example.com"));
        Assert.That(repository.SavedLead.Phone, Is.EqualTo("447000000000"));
    }

    [Test]
    public async Task update_site_fields_saves_owner_defined_field_definitions()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var siteRepository = new InMemorySiteRepository();
        var controller = CreateController(repository, siteRepository, siteId);

        var result = await controller.UpdateSiteFields(
            new List<OwnerPortalController.OwnerFieldInputModel>
            {
                new() { Id = "project_overview", Name = "Project overview", Description = "Scope and goals" },
                new() { Id = "budget", Name = "Budget", Description = "Budget range" }
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var updatedSite = await siteRepository.GetByIdAsync(siteId, CancellationToken.None);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite!.Fields.Count, Is.EqualTo(2));
        Assert.That(updatedSite.Fields[0].Id, Is.EqualTo("project_overview"));
        Assert.That(updatedSite.Fields[1].Id, Is.EqualTo("budget"));
    }

    [Test]
    public async Task update_site_fields_rejects_duplicate_ids()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var siteRepository = new InMemorySiteRepository();
        var before = await siteRepository.GetByIdAsync(siteId, CancellationToken.None);
        var beforeCount = before?.Fields.Count ?? 0;
        var controller = CreateController(repository, siteRepository, siteId);

        var result = await controller.UpdateSiteFields(
            new List<OwnerPortalController.OwnerFieldInputModel>
            {
                new() { Id = "project_overview", Name = "Project overview", Description = "Scope and goals" },
                new() { Id = "project_overview", Name = "Duplicate", Description = "Duplicate id" }
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var view = (ViewResult)result;
        var model = view.Model as OwnerPortalController.OwnerSiteSettingsModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.Error, Is.EqualTo("Field ids must be unique."));

        var after = await siteRepository.GetByIdAsync(siteId, CancellationToken.None);
        Assert.That(after, Is.Not.Null);
        Assert.That(after!.Fields.Count, Is.EqualTo(beforeCount));
    }

    [Test]
    public async Task set_paused_updates_bot_pause_flag()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.SetPaused(lead.Id, true, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.SavedLead, Is.Not.Null);
        Assert.That(repository.SavedLead!.IsBotPaused, Is.True);
    }

    [Test]
    public async Task set_paused_returns_ok_for_ajax_requests()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);
        controller.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.SetPaused(lead.Id, true, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(repository.SavedLead, Is.Not.Null);
        Assert.That(repository.SavedLead!.IsBotPaused, Is.True);
    }

    private static Lead BuildLead(string siteId)
    {
        return new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Channel = "api",
            CustomerId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid()
        };
    }

    private static OwnerPortalController CreateController(FakeLeadRepository repository, ISiteRepository siteRepository, string siteId)
    {
        var controller = new OwnerPortalController(repository, new RecordingDispatcher(), siteRepository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Items[OwnerAuthMiddleware.ContextKey] = new OwnerAuthContext(siteId, "owner@example.com");
        return controller;
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
