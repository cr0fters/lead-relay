using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace LeadRelay.Web.Leads;

public sealed class LeadCaptureService(ILeadRepository leads, IEmailSender emailSender, LeadRelayDbContext db)
{
    public async Task<LeadCaptureResult> CaptureAsync(
        Site site,
        LeadCaptureInput input,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var leadId = input.LeadId ?? Guid.NewGuid();
        var leadJustCreated = input.LeadId is null || input.NotifyOwner;

        var existingLead = input.LeadId is null
            ? null
            : await leads.GetByIdForSiteAsync(input.LeadId.Value, site.Id, ct);

        var lead = new Lead
        {
            Id = leadId,
            SiteId = site.Id,
            CreatedAtUtc = input.LeadCreatedAtUtc ?? now,
            Channel = NormalizeChannel(input.Channel),
            Status = LeadStatuses.Open,
        };
        string? projectSummaryForLead = null;

        lead.Name = input.ExplicitName?.Trim() ?? ExtractName(input.Fields) ?? input.ContactName ?? lead.Name;
        lead.Email = input.ExplicitEmail?.Trim() ?? ExtractEmail(input.Fields) ?? lead.Email;
        lead.Phone = NormalizePhone(input.ExplicitPhone ?? "") ?? ExtractPhone(input.Fields) ?? lead.Phone;
        if (string.IsNullOrWhiteSpace(lead.Phone))
            lead.Phone = NormalizePhone(input.ExternalContactId ?? "");

        var customerId = existingLead?.CustomerId;
        if (!customerId.HasValue || customerId.Value == Guid.Empty)
        {
            var customer = await ResolveOrCreateCustomerAsync(
                site.Id,
                input.ExternalContactId,
                lead.Name,
                lead.Email,
                lead.Phone,
                now,
                ct);
            customerId = customer.Id;
        }
        var effectiveCustomerId = customerId ?? throw new InvalidOperationException("CustomerId resolution failed.");

        var projectId = existingLead?.ProjectId;
        if (!projectId.HasValue || projectId.Value == Guid.Empty)
        {
            var project = CreateProject(site.Id, effectiveCustomerId, input, now);
            db.Projects.Add(project);
            projectId = project.Id;
            projectSummaryForLead = project.Summary;
        }
        else
        {
            var existingProject = await db.Projects.FirstOrDefaultAsync(
                x => x.SiteId == site.Id && x.Id == projectId.Value,
                ct);
            if (existingProject is not null)
            {
                MergeProjectFields(existingProject, input.Fields);
                existingProject.Summary = ResolveProjectSummary(input, existingProject.Summary);
                existingProject.UpdatedAtUtc = now;
                projectSummaryForLead = existingProject.Summary;
            }
        }

        lead.CustomerId = effectiveCustomerId;
        lead.ProjectId = projectId ?? throw new InvalidOperationException("ProjectId resolution failed.");
        lead.ProjectSummary = projectSummaryForLead;

        foreach (var turn in input.Conversation)
            lead.Conversation.Add(new LeadConversationTurn(turn.Role, turn.Text, turn.AtUtc));

        await db.SaveChangesAsync(ct);
        await leads.SaveAsync(lead, ct);

        if (input.NotifyOwner)
        {
            var fieldsBlock = string.Join("\n", input.Fields.Select(kv => $"{kv.Key}: {kv.Value}"));
            var body = $"New lead for {site.Name}\n\nChannel: {lead.Channel}\nFields:\n{fieldsBlock}\n";
            await emailSender.SendAsync(site.OwnerEmail, $"New lead ({site.Name})", body, ct);
        }

        return new LeadCaptureResult(lead, leadJustCreated, true);
    }

    private async Task<CustomerRecord> ResolveOrCreateCustomerAsync(
        string siteId,
        string? externalContactId,
        string? name,
        string? email,
        string? phone,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var normalizedExternalId = NormalizeString(externalContactId);
        var normalizedEmail = NormalizeString(email);
        var normalizedName = NormalizeString(name);
        var normalizedPhone = NormalizePhone(phone ?? "");

        CustomerRecord? customer = null;
        if (!string.IsNullOrWhiteSpace(normalizedExternalId))
        {
            customer = await db.Customers.FirstOrDefaultAsync(
                x => x.SiteId == siteId && x.ExternalContactId == normalizedExternalId,
                ct);
        }

        if (customer is null && !string.IsNullOrWhiteSpace(normalizedPhone))
        {
            customer = await db.Customers.FirstOrDefaultAsync(
                x => x.SiteId == siteId && x.Phone == normalizedPhone,
                ct);
        }

        if (customer is null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            customer = await db.Customers.FirstOrDefaultAsync(
                x => x.SiteId == siteId && x.Email == normalizedEmail,
                ct);
        }

        if (customer is null)
        {
            customer = new CustomerRecord
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                Name = normalizedName,
                Email = normalizedEmail,
                Phone = normalizedPhone,
                ExternalContactId = normalizedExternalId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Customers.Add(customer);
            return customer;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(normalizedName))
        {
            customer.Name = normalizedName;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(customer.Email) && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            customer.Email = normalizedEmail;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(customer.Phone) && !string.IsNullOrWhiteSpace(normalizedPhone))
        {
            customer.Phone = normalizedPhone;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(customer.ExternalContactId) && !string.IsNullOrWhiteSpace(normalizedExternalId))
        {
            customer.ExternalContactId = normalizedExternalId;
            changed = true;
        }
        if (changed)
            customer.UpdatedAtUtc = now;

        return customer;
    }

    private static ProjectRecord CreateProject(
        string siteId,
        Guid customerId,
        LeadCaptureInput input,
        DateTimeOffset now)
    {
        var summary = ResolveProjectSummary(input, null);
        var projectName = BuildProjectName(input, summary, now);
        var fields = input.Fields
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Where(x => !string.Equals(x.Key.Trim(), "project_summary", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                x => x.Key.Trim(),
                x => (x.Value ?? "").Trim(),
                StringComparer.OrdinalIgnoreCase);

        return new ProjectRecord
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            CustomerId = customerId,
            Name = projectName,
            Summary = summary,
            Status = ProjectStatuses.New,
            Fields = fields,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static string BuildProjectName(LeadCaptureInput input, string? summary, DateTimeOffset now)
    {
        var projectDescription = GetFieldValue(input.Fields, "project_overview", "project_description", "project", "scope", "brief");
        if (!string.IsNullOrWhiteSpace(projectDescription))
            return projectDescription.Length > 64 ? projectDescription[..64] : projectDescription;

        if (!string.IsNullOrWhiteSpace(summary))
            return summary.Length > 64 ? summary[..64] : summary;

        var message = NormalizeString(input.FallbackMessage);
        if (!string.IsNullOrWhiteSpace(message))
            return message.Length > 64 ? message[..64] : message;

        return $"Inbound {input.Channel} {now:yyyy-MM-dd}";
    }

    private static string? ResolveProjectSummary(LeadCaptureInput input, string? existingSummary)
    {
        var providedSummary = NormalizeString(input.ProjectSummary);
        if (IsMeaningfulSummaryText(providedSummary))
            return providedSummary;

        var projectOverview = GetFieldValue(input.Fields, "project_overview", "project_description", "project", "scope", "brief");
        if (IsMeaningfulSummaryText(projectOverview))
            return NormalizeString(projectOverview);

        var firstUserMessage = ExtractFirstUserMessage(input.Conversation);
        if (IsMeaningfulSummaryText(firstUserMessage))
            return NormalizeString(firstUserMessage);

        var fallbackMessage = NormalizeString(input.FallbackMessage);
        if (IsMeaningfulSummaryText(fallbackMessage))
            return fallbackMessage;

        return existingSummary;
    }

    private static void MergeProjectFields(ProjectRecord project, IReadOnlyDictionary<string, string> updates)
    {
        foreach (var pair in updates)
        {
            var key = (pair.Key ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (string.Equals(key, "project_summary", StringComparison.OrdinalIgnoreCase))
                continue;

            project.Fields[key] = (pair.Value ?? "").Trim();
        }
    }

    private static string? NormalizeString(string? input)
    {
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    private static string NormalizeChannel(string? channel)
        => string.IsNullOrWhiteSpace(channel) ? "api" : channel.Trim().ToLowerInvariant();

    private static string? ExtractFirstUserMessage(IReadOnlyList<LeadCaptureTurn> history)
    {
        for (var i = 0; i < history.Count; i++)
        {
            var turn = history[i];
            if (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
                return turn.Text;
        }

        return null;
    }

    private static string? ExtractName(IReadOnlyDictionary<string, string> fields)
    {
        return GetFieldValue(fields, "name", "full_name", "fullname", "fullName", "first_name", "last_name");
    }

    private static string? ExtractEmail(IReadOnlyDictionary<string, string> fields)
    {
        return GetFieldValue(fields, "email", "email_address");
    }

    private static string? ExtractPhone(IReadOnlyDictionary<string, string> fields)
    {
        return NormalizePhone(GetFieldValue(fields, "phone", "phone_number", "mobile", "mobile_number") ?? "");
    }

    private static string? GetFieldValue(IReadOnlyDictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string? NormalizePhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : null;
    }

    private static bool IsMeaningfulSummaryText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.Length < 4)
            return false;

        return !IsGreetingOnly(normalized);
    }

    private static bool IsGreetingOnly(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return normalized is "hi" or "hello" or "hey" or "yo" or "hiya" or "sup";
    }
}
