using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.WhatsApp;

namespace LeadRelay.Web.Leads;

public sealed class LeadCaptureService(ILeadRepository leads, IEmailSender emailSender)
{
    public async Task<LeadCaptureResult> CaptureAsync(
        Site site,
        string waId,
        string? intent,
        string? fallbackMessage,
        ConversationReply reply,
        string? contactName,
        CancellationToken ct)
    {
        if (reply.LeadId is null)
            return new LeadCaptureResult(null, reply.LeadJustCreated, false);

        var lead = new Lead
        {
            Id = reply.LeadId.Value,
            SiteId = site.Id,
            CreatedAtUtc = reply.LeadCreatedAtUtc ?? DateTimeOffset.UtcNow,
            Intent = intent,
            Notes = $"waId={waId}; firstMessage={ExtractFirstUserMessage(reply.History) ?? fallbackMessage}"
        };

        foreach (var kv in reply.Collected)
            lead.Fields[kv.Key] = kv.Value;

        lead.Name = ExtractName(reply.Collected) ?? contactName ?? lead.Name;
        lead.Email = ExtractEmail(reply.Collected) ?? lead.Email;
        lead.Phone = ExtractPhone(reply.Collected) ?? lead.Phone;
        if (string.IsNullOrWhiteSpace(lead.Phone))
            lead.Phone = NormalizePhone(waId);

        foreach (var turn in reply.History)
            lead.Conversation.Add(new LeadConversationTurn(turn.Role, turn.Text, turn.AtUtc));

        await leads.SaveAsync(lead, ct);

        if (reply.LeadJustCreated)
        {
            var fieldsBlock = string.Join("\n", lead.Fields.Select(kv => $"{kv.Key}: {kv.Value}"));
            var body = $"New lead for {site.Name}\n\nFields:\n{fieldsBlock}\n\nNotes: {lead.Notes}\n";
            await emailSender.SendAsync(site.OwnerEmail, $"New WhatsApp lead ({site.Name})", body, ct);
        }

        return new LeadCaptureResult(lead, reply.LeadJustCreated, true);
    }

    private static string? ExtractFirstUserMessage(IReadOnlyList<ConversationTurn> history)
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
