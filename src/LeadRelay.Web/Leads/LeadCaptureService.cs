using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
namespace LeadRelay.Web.Leads;

public sealed class LeadCaptureService(ILeadRepository leads, IEmailSender emailSender)
{
    public async Task<LeadCaptureResult> CaptureAsync(
        Site site,
        LeadCaptureInput input,
        CancellationToken ct)
    {
        var leadId = input.LeadId ?? Guid.NewGuid();
        var leadJustCreated = input.LeadId is null || input.NotifyOwner;

        var lead = new Lead
        {
            Id = leadId,
            SiteId = site.Id,
            CreatedAtUtc = input.LeadCreatedAtUtc ?? DateTimeOffset.UtcNow,
            Intent = input.Intent,
            Notes = BuildNotes(input)
        };

        foreach (var kv in input.Fields)
            lead.Fields[kv.Key] = kv.Value;

        lead.Name = input.ExplicitName?.Trim() ?? ExtractName(input.Fields) ?? input.ContactName ?? lead.Name;
        lead.Email = input.ExplicitEmail?.Trim() ?? ExtractEmail(input.Fields) ?? lead.Email;
        lead.Phone = NormalizePhone(input.ExplicitPhone ?? "") ?? ExtractPhone(input.Fields) ?? lead.Phone;
        if (string.IsNullOrWhiteSpace(lead.Phone))
            lead.Phone = NormalizePhone(input.ExternalContactId ?? "");

        foreach (var turn in input.Conversation)
            lead.Conversation.Add(new LeadConversationTurn(turn.Role, turn.Text, turn.AtUtc));

        await leads.SaveAsync(lead, ct);

        if (input.NotifyOwner)
        {
            var fieldsBlock = string.Join("\n", lead.Fields.Select(kv => $"{kv.Key}: {kv.Value}"));
            var body = $"New lead for {site.Name}\n\nChannel: {input.Channel}\nFields:\n{fieldsBlock}\n\nNotes: {lead.Notes}\n";
            await emailSender.SendAsync(site.OwnerEmail, $"New lead ({site.Name})", body, ct);
        }

        return new LeadCaptureResult(lead, leadJustCreated, true);
    }

    private static string BuildNotes(LeadCaptureInput input)
    {
        var firstMessage = ExtractFirstUserMessage(input.Conversation) ?? input.FallbackMessage ?? "";
        var channel = (input.Channel ?? "").Trim().ToLowerInvariant();
        var externalId = (input.ExternalContactId ?? "").Trim();
        return $"channel={channel}; externalId={externalId}; firstMessage={firstMessage}";
    }

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
}
