using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Controllers;
using LeadRelay.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Text;

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
        Assert.That(dispatcher.SiteId, Is.EqualTo(siteId));
    }

    [Test]
    public async Task reply_persists_outbound_message_in_conversation()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Email = "lead@example.com",
            Channel = "email",
            CustomerId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid()
        };

        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.Reply(lead.Id, "Thanks for the update", "email", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.SavedLead, Is.Not.Null);
        Assert.That(repository.SavedLead!.Conversation.Count, Is.EqualTo(1));
        Assert.That(repository.SavedLead.Conversation[0].Role, Is.EqualTo("owner"));
        Assert.That(repository.SavedLead.Conversation[0].Text, Is.EqualTo("Thanks for the update"));
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
        Assert.That(updatedSite.WhatsAppPhoneNumberId, Is.EqualTo("demo-phone-number-id"));
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

    [Test]
    public async Task update_stage_persists_owner_stage_and_adds_timeline_activity()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.UpdateStage(lead.Id, ProjectStatuses.Qualified, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(lead.ProjectStage, Is.EqualTo(ProjectStatuses.Qualified));
        Assert.That(lead.ProjectStageChanges, Has.Count.EqualTo(1));
        var model = ((ViewResult)result).Model as OwnerPortalController.OwnerLeadDetailModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.Activity.Any(x => x.Kind == "stage" && x.Text.Contains("New to Qualified")), Is.True);
    }

    [Test]
    public async Task update_stage_rejects_unknown_stage_without_changing_lead()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.UpdateStage(lead.Id, "deleted", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(lead.ProjectStage, Is.EqualTo(ProjectStatuses.New));
        Assert.That(lead.ProjectStageChanges, Is.Empty);
        var model = ((ViewResult)result).Model as OwnerPortalController.OwnerLeadDetailModel;
        Assert.That(model?.Error, Is.EqualTo("Choose a valid lead stage."));
    }

    [Test]
    public async Task inbox_passes_stage_and_inclusive_utc_date_filters_to_repository()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var repository = new FakeLeadRepository(BuildLead(siteId));
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.Index(
            q: "jane",
            stage: ProjectStatuses.Contacted,
            from: "2026-08-01",
            to: "2026-08-18",
            page: 2,
            pageSize: 50,
            ct: CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.LastSearchCriteria, Is.Not.Null);
        Assert.That(repository.LastSearchCriteria!.Query, Is.EqualTo("jane"));
        Assert.That(repository.LastSearchCriteria.ProjectStage, Is.EqualTo(ProjectStatuses.Contacted));
        Assert.That(repository.LastSearchCriteria.CreatedFromUtc,
            Is.EqualTo(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(repository.LastSearchCriteria.CreatedBeforeUtc,
            Is.EqualTo(new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(repository.LastSearchCriteria.Page, Is.EqualTo(2));
        Assert.That(repository.LastSearchCriteria.PageSize, Is.EqualTo(50));
        var model = ((ViewResult)result).Model as OwnerPortalController.OwnerDashboardModel;
        Assert.That(model?.HasActiveFilters, Is.True);
    }

    [Test]
    public async Task unfiltered_empty_inbox_is_identified_as_a_first_run_state()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var repository = new FakeLeadRepository(BuildLead(siteId));
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.Index(ct: CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = ((ViewResult)result).Model as OwnerPortalController.OwnerDashboardModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.Leads, Is.Empty);
        Assert.That(model.HasActiveFilters, Is.False);
    }

    [Test]
    public async Task opening_a_lead_marks_it_viewed_for_the_authenticated_site()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.Lead(lead.Id, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(repository.ViewedLeadId, Is.EqualTo(lead.Id));
        Assert.That(repository.ViewedSiteId, Is.EqualTo(siteId));
        Assert.That(repository.ViewedAtUtc, Is.Not.Null);
    }

    [Test]
    public async Task update_follow_up_saves_trimmed_notes_action_and_utc_due_date()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.UpdateFollowUp(
            lead.Id,
            "  Decision maker prefers email.  ",
            "  Send proposal  ",
            "2026-08-20T09:30",
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(lead.OwnerNotes, Is.EqualTo("Decision maker prefers email."));
        Assert.That(lead.NextAction, Is.EqualTo("Send proposal"));
        Assert.That(lead.NextActionAtUtc,
            Is.EqualTo(new DateTimeOffset(2026, 8, 20, 9, 30, 0, TimeSpan.Zero)));
    }

    [Test]
    public async Task update_follow_up_requires_an_action_for_a_due_date()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var lead = BuildLead(siteId);
        var repository = new FakeLeadRepository(lead);
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.UpdateFollowUp(
            lead.Id,
            null,
            null,
            "2026-08-20T09:30",
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(lead.NextActionAtUtc, Is.Null);
        var model = ((ViewResult)result).Model as OwnerPortalController.OwnerLeadDetailModel;
        Assert.That(model?.Error, Is.EqualTo("Add a next action before setting its due date."));
    }

    [Test]
    public async Task export_returns_utf8_csv_for_the_authenticated_site()
    {
        var siteId = InMemorySiteRepository.DefaultSiteId;
        var repository = new FakeLeadRepository(BuildLead(siteId));
        repository.ExportRows =
        [
            new LeadExportRow(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Jane",
                "jane@example.com",
                null,
                "whatsapp",
                false,
                ProjectStatuses.New,
                null,
                null,
                null,
                null,
                new Dictionary<string, string>())
        ];
        var controller = CreateController(repository, new InMemorySiteRepository(), siteId);

        var result = await controller.Export(CancellationToken.None);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = (FileContentResult)result;
        Assert.That(file.ContentType, Is.EqualTo("text/csv; charset=utf-8"));
        Assert.That(file.FileContents.Take(3), Is.EqualTo(Encoding.UTF8.GetPreamble()));
        Assert.That(Encoding.UTF8.GetString(file.FileContents), Does.Contain("jane@example.com"));
        Assert.That(repository.ExportSiteId, Is.EqualTo(siteId));
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
        public LeadSearchCriteria? LastSearchCriteria { get; private set; }
        public IReadOnlyList<LeadExportRow> ExportRows { get; set; } = Array.Empty<LeadExportRow>();
        public string? ExportSiteId { get; private set; }
        public Guid? ViewedLeadId { get; private set; }
        public string? ViewedSiteId { get; private set; }
        public DateTimeOffset? ViewedAtUtc { get; private set; }

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

        public Task<LeadPageResult> SearchBySiteAsync(string siteId, LeadSearchCriteria criteria, CancellationToken ct)
        {
            LastSearchCriteria = criteria;
            return Task.FromResult(new LeadPageResult(Array.Empty<LeadSummary>(), 0, 0, criteria.Page, criteria.PageSize));
        }

        public Task<IReadOnlyList<LeadExportRow>> GetExportBySiteAsync(string siteId, CancellationToken ct)
        {
            ExportSiteId = siteId;
            return Task.FromResult(ExportRows);
        }

        public Task<bool> UpdateProjectStageAsync(Guid leadId, string siteId, string stage, DateTimeOffset changedAtUtc, CancellationToken ct)
        {
            if (_lead.Id != leadId || _lead.SiteId != siteId)
                return Task.FromResult(false);

            var previousStage = ProjectStatuses.Normalize(_lead.ProjectStage);
            if (!string.Equals(previousStage, stage, StringComparison.Ordinal))
            {
                _lead.ProjectStage = stage;
                _lead.ProjectStageChanges.Add(new ProjectStageChange(previousStage, stage, changedAtUtc));
            }
            return Task.FromResult(true);
        }

        public Task<bool> UpdateProjectFollowUpAsync(
            Guid leadId,
            string siteId,
            string? ownerNotes,
            string? nextAction,
            DateTimeOffset? nextActionAtUtc,
            DateTimeOffset updatedAtUtc,
            CancellationToken ct)
        {
            if (_lead.Id != leadId || _lead.SiteId != siteId)
                return Task.FromResult(false);

            _lead.OwnerNotes = ownerNotes;
            _lead.NextAction = nextAction;
            _lead.NextActionAtUtc = nextAction is null ? null : nextActionAtUtc;
            return Task.FromResult(true);
        }

        public Task<bool> MarkViewedAsync(Guid leadId, string siteId, DateTimeOffset viewedAtUtc, CancellationToken ct)
        {
            if (_lead.Id != leadId || _lead.SiteId != siteId)
                return Task.FromResult(false);

            _lead.OwnerViewedAtUtc ??= viewedAtUtc;
            ViewedLeadId = leadId;
            ViewedSiteId = siteId;
            ViewedAtUtc = viewedAtUtc;
            return Task.FromResult(true);
        }

        public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Lead?>(_lead.Id == id ? _lead : null);

        public Task<Lead?> GetByIdForSiteAsync(Guid id, string siteId, CancellationToken ct)
            => Task.FromResult<Lead?>(_lead.Id == id && _lead.SiteId == siteId ? _lead : null);
    }

    private sealed class RecordingDispatcher : IMessageDispatcher
    {
        public string? Channel { get; private set; }
        public string? Recipient { get; private set; }
        public string? SiteId { get; private set; }

        public Task<MessageDispatchResult> SendTextAsync(string channel, string recipient, string text, string? siteId, CancellationToken ct)
        {
            Channel = channel;
            Recipient = recipient;
            SiteId = siteId;
            return Task.FromResult(new MessageDispatchResult(true));
        }
    }
}
