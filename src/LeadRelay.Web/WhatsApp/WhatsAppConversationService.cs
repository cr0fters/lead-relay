using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppConversationService(IClock clock)
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new(StringComparer.Ordinal);

    public ConversationReply HandleMessage(Site site, string waId, string? text)
    {
        var normalizedText = (text ?? "").Trim();
        var key = $"{site.Id}:{waId}";

        if (!_states.TryGetValue(key, out var state))
        {
            state = new ConversationState(site.Id, 0, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), clock.UtcNow);
            _states[key] = state;

            var firstPrompt = GetPrompt(site, state.StepIndex);
            return new ConversationReply(BuildIntro(site, firstPrompt), false, new Dictionary<string, string>());
        }

        var field = GetField(site, state.StepIndex);
        if (field is null)
        {
            _states.TryRemove(key, out _);
            return new ConversationReply("Thanks! We’ll be in touch shortly.", true, state.Collected);
        }

        if (!TryAcceptField(field, normalizedText, out var value, out var errorReply))
        {
            return new ConversationReply(errorReply, false, state.Collected);
        }

        state.Collected[field.Key] = value;
        state = state with { StepIndex = state.StepIndex + 1, UpdatedAtUtc = clock.UtcNow };
        _states[key] = state;

        var nextPrompt = GetPrompt(site, state.StepIndex);
        if (nextPrompt is null)
        {
            _states.TryRemove(key, out _);
            return new ConversationReply("Thanks! We’ll be in touch shortly.", true, state.Collected);
        }

        return new ConversationReply(nextPrompt, false, state.Collected);
    }

    private static string BuildIntro(Site site, string? nextPrompt)
    {
        if (string.IsNullOrWhiteSpace(nextPrompt))
            return "Thanks for reaching out!";

        return $"{(string.IsNullOrWhiteSpace(site.Name) ? "Thanks for reaching out" : $"Thanks for reaching out to {site.Name}")}! {nextPrompt}";
    }

    private static ConversationField? GetField(Site site, int index)
    {
        if (site.Fields.Count == 0) return null;
        if (index < 0 || index >= site.Fields.Count) return null;
        return site.Fields[index];
    }

    private static string? GetPrompt(Site site, int index)
    {
        var field = GetField(site, index);
        return field?.Prompt;
    }

    private static bool TryAcceptField(ConversationField field, string input, out string value, out string errorReply)
    {
        value = "";
        errorReply = field.Prompt;

        if (string.IsNullOrWhiteSpace(input))
            return !field.Required;

        switch (field.Type)
        {
            case ConversationFieldType.Email:
                if (!TryExtractEmail(input, out var email))
                {
                    errorReply = "Could you share a valid email address?";
                    return false;
                }
                value = email;
                return true;
            case ConversationFieldType.Phone:
                value = NormalizePhone(input);
                if (string.IsNullOrWhiteSpace(value))
                {
                    errorReply = "Could you share a valid phone number?";
                    return false;
                }
                return true;
            default:
                value = input.Trim();
                return true;
        }
    }

    private static bool TryExtractEmail(string input, out string email)
    {
        email = "";
        if (string.IsNullOrWhiteSpace(input)) return false;

        var match = Regex.Match(input, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        email = match.Value;
        return true;
    }

    private static string NormalizePhone(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : "";
    }
}

public sealed record ConversationReply(
    string ReplyText,
    bool IsComplete,
    Dictionary<string, string> Collected);

public sealed record ConversationState(
    string SiteId,
    int StepIndex,
    Dictionary<string, string> Collected,
    DateTimeOffset UpdatedAtUtc);
