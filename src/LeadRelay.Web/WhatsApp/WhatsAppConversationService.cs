using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.AI;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppConversationService(
    IClock clock,
    OpenAIClient openAi,
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ConversationOptions> conversationOptions,
    ILogger<WhatsAppConversationService> logger)
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ConversationReply> HandleMessageAsync(
        Site site,
        string waId,
        string? text,
        string? systemPromptOverride,
        CancellationToken ct)
    {
        var normalizedText = (text ?? "").Trim();
        var key = $"{site.Id}:{waId}";

        if (!_states.TryGetValue(key, out var state))
        {
            state = new ConversationState(
                site.Id,
                0,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                clock.UtcNow,
                new List<ConversationTurn>(),
                NormalizeOverride(systemPromptOverride),
                null,
                null);
            _states[key] = state;

            if (!string.IsNullOrWhiteSpace(normalizedText))
                AppendTurn(state, "user", normalizedText);

            var leadJustCreated = false;
            if (conversationOptions.Value.SubmitLeadOnFirstMessage)
            {
                (state, leadJustCreated) = EnsureLead(state, normalizedText);
                _states[key] = state;
            }

            var firstPrompt = GetPrompt(site, state.StepIndex);
            var intro = BuildIntro(site, firstPrompt);
            AppendTurn(state, "assistant", intro);
            return new ConversationReply(
                intro,
                false,
                new Dictionary<string, string>(),
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated);
        }

        AppendTurn(state, "user", normalizedText);

        var normalizedOverride = NormalizeOverride(systemPromptOverride);
        if (!string.IsNullOrWhiteSpace(normalizedOverride) &&
            !string.Equals(state.SystemPromptOverride, normalizedOverride, StringComparison.Ordinal))
        {
            state = state with { SystemPromptOverride = normalizedOverride, UpdatedAtUtc = clock.UtcNow };
            _states[key] = state;
        }

        var leadJustCreatedExisting = false;
        if (conversationOptions.Value.SubmitLeadOnFirstMessage)
        {
            (state, leadJustCreatedExisting) = EnsureLead(state, normalizedText);
            _states[key] = state;
        }

        if (conversationOptions.Value.UseLlm)
        {
            var llmReply = await TryHandleWithLlmAsync(site, key, state, normalizedText, ct);
            if (llmReply is not null)
                return llmReply with
                {
                    History = state.History.ToList(),
                    LeadId = state.LeadId,
                    LeadCreatedAtUtc = state.LeadCreatedAtUtc,
                    LeadJustCreated = leadJustCreatedExisting
                };
        }

        return HandleDeterministic(site, key, state, normalizedText, leadJustCreatedExisting);
    }

    private ConversationReply HandleDeterministic(
        Site site,
        string key,
        ConversationState state,
        string normalizedText,
        bool leadJustCreated)
    {
        var field = GetField(site, state.StepIndex);
        if (field is null)
        {
            var completedReply = "Thanks! We’ll be in touch shortly.";
            AppendTurn(state, "assistant", completedReply);
            return new ConversationReply(
                completedReply,
                true,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated);
        }

        if (!TryAcceptField(field, normalizedText, out var value, out var errorReply))
        {
            AppendTurn(state, "assistant", errorReply);
            return new ConversationReply(
                errorReply,
                false,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated);
        }

        state.Collected[field.Key] = value;
        state = state with { StepIndex = state.StepIndex + 1, UpdatedAtUtc = clock.UtcNow };
        _states[key] = state;

        var nextPrompt = GetPrompt(site, state.StepIndex);
        if (nextPrompt is null)
        {
            var completedReply = "Thanks! We’ll be in touch shortly.";
            AppendTurn(state, "assistant", completedReply);
            return new ConversationReply(
                completedReply,
                true,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated);
        }

        AppendTurn(state, "assistant", nextPrompt);
        return new ConversationReply(
            nextPrompt,
            false,
            state.Collected,
            state.History.ToList(),
            state.LeadId,
            state.LeadCreatedAtUtc,
            leadJustCreated);
    }

    private async Task<ConversationReply?> TryHandleWithLlmAsync(
        Site site,
        string key,
        ConversationState state,
        string normalizedText,
        CancellationToken ct)
    {
        var options = conversationOptions.Value;
        var openAiSettings = openAiOptions.Value;

        var systemPrompt = BuildSystemPrompt(site, options, state.SystemPromptOverride);
        var userPrompt = BuildUserPrompt(state, normalizedText, options.MaxHistoryTurns);

        var payload = new
        {
            model = openAiSettings.Model,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new[] { new { type = "input_text", text = systemPrompt } }
                },
                new
                {
                    role = "user",
                    content = new[] { new { type = "input_text", text = userPrompt } }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "lead_reply",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            reply_text = new { type = "string" },
                            collected = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    properties = new
                                    {
                                        key = new { type = "string" },
                                        value = new { type = "string" }
                                    },
                                    required = new[] { "key", "value" }
                                }
                            },
                            done = new { type = "boolean" }
                        },
                        required = new[] { "reply_text", "collected", "done" }
                    }
                }
            },
            temperature = openAiSettings.Temperature,
            max_output_tokens = openAiSettings.MaxOutputTokens
        };

        var json = await openAi.CreateJsonResponseAsync(payload, ct);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        LlmReply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<LlmReply>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse OpenAI response JSON.");
            return null;
        }

        if (reply is null || string.IsNullOrWhiteSpace(reply.ReplyText))
            return null;

        var proposed = reply.Collected?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => item.Value ?? "", StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var merged = MergeCollected(site, state.Collected, proposed);
        merged = TryInferFromCurrentField(site, state, normalizedText, merged);
        var requiredComplete = AreRequiredFieldsFilled(site, merged);
        var done = reply.Done || (requiredComplete && site.OptionalFields.Count == 0);
        var replyText = reply.ReplyText.Trim();

        AppendTurn(state, "assistant", replyText);

        state = state with { Collected = merged, UpdatedAtUtc = clock.UtcNow };
        _states[key] = state;

        if (done)
        {
            return new ConversationReply(
                replyText,
                true,
                merged,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                false);
        }

        var nextIndex = GetNextStepIndex(site, merged);
        state = state with { StepIndex = nextIndex, UpdatedAtUtc = clock.UtcNow };
        _states[key] = state;

        return new ConversationReply(
            replyText,
            false,
            merged,
            state.History.ToList(),
            state.LeadId,
            state.LeadCreatedAtUtc,
            false);
    }

    private static string BuildSystemPrompt(Site site, ConversationOptions options, string? systemPromptOverride)
    {
        var businessSummary = string.IsNullOrWhiteSpace(site.BusinessSummary) ? "Not provided." : site.BusinessSummary.Trim();
        var baseTemplate = string.IsNullOrWhiteSpace(systemPromptOverride)
            ? options.GlobalPromptTemplate
            : systemPromptOverride;

        var template = baseTemplate
            .Replace("{business_summary}", businessSummary, StringComparison.OrdinalIgnoreCase)
            .Replace("{site_name}", site.Name, StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine(template);
        sb.AppendLine();
        sb.AppendLine("Required fields (key | type | required | prompt):");
        foreach (var field in site.Fields)
            sb.AppendLine($"- {field.Key} | {field.Type} | {field.Required} | {field.Prompt}");

        sb.AppendLine();
        if (site.OptionalFields.Count > 0)
        {
            sb.AppendLine("Nice-to-have fields (optional):");
            foreach (var field in site.OptionalFields)
                sb.AppendLine($"- {field.Key} | {field.Type} | {field.Prompt}");
            sb.AppendLine();
        }

        sb.AppendLine("Rules:");
        sb.AppendLine("- Be concise, friendly, and conversational.");
        sb.AppendLine("- Ask at most one question per reply.");
        sb.AppendLine("- If the user already provided a field, do not ask again.");
        sb.AppendLine("- If missing required fields, ask for one missing field.");
        sb.AppendLine("- Optional fields are nice-to-have; never block completion.");
        sb.AppendLine("- If the user goes off-topic, answer briefly and steer back.");

        return sb.ToString();
    }

    private static string BuildUserPrompt(ConversationState state, string normalizedText, int maxHistoryTurns)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User message: {normalizedText}");
        sb.AppendLine();
        sb.AppendLine("Collected so far (key: value):");
        if (state.Collected.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var pair in state.Collected)
                sb.AppendLine($"- {pair.Key}: {pair.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Recent conversation:");
        if (state.History.Count == 0)
        {
            sb.AppendLine("- none");
            return sb.ToString();
        }

        var history = state.History.TakeLast(Math.Max(1, maxHistoryTurns));
        foreach (var turn in history)
            sb.AppendLine($"- {turn.Role}: {turn.Text}");

        return sb.ToString();
    }

    private static Dictionary<string, string> MergeCollected(
        Site site,
        Dictionary<string, string> existing,
        Dictionary<string, string> proposed)
    {
        var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var field in EnumerateAllFields(site))
        {
            if (!proposed.TryGetValue(field.Key, out var raw))
                continue;

            if (!TryAcceptField(field, raw, out var value, out _))
                continue;

            merged[field.Key] = value;
        }

        return merged;
    }

    private static IEnumerable<ConversationField> EnumerateAllFields(Site site)
    {
        foreach (var field in site.Fields)
            yield return field;

        foreach (var field in site.OptionalFields)
            yield return field;
    }

    private static int GetNextStepIndex(Site site, Dictionary<string, string> collected)
    {
        for (var i = 0; i < site.Fields.Count; i++)
        {
            if (!collected.ContainsKey(site.Fields[i].Key))
                return i;
        }

        return site.Fields.Count;
    }

    private static bool AreRequiredFieldsFilled(Site site, Dictionary<string, string> collected)
    {
        foreach (var field in site.Fields)
        {
            if (!field.Required)
                continue;

            if (!collected.ContainsKey(field.Key))
                return false;
        }

        return true;
    }

    private static Dictionary<string, string> TryInferFromCurrentField(
        Site site,
        ConversationState state,
        string normalizedText,
        Dictionary<string, string> collected)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
            return collected;

        if (state.StepIndex == 0 && state.History.Count <= 1)
            return collected;

        if (IsGreetingOnly(normalizedText))
            return collected;

        if (normalizedText.Length < 6)
            return collected;

        var field = GetField(site, state.StepIndex);
        if (field is null)
            return collected;

        if (collected.ContainsKey(field.Key))
            return collected;

        if (!TryAcceptField(field, normalizedText, out var value, out _))
            return collected;

        var updated = new Dictionary<string, string>(collected, StringComparer.OrdinalIgnoreCase)
        {
            [field.Key] = value
        };

        return updated;
    }

    private static bool IsGreetingOnly(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return normalized is "hi" or "hello" or "hey" or "yo" or "hiya" or "sup";
    }

    private void AppendTurn(ConversationState state, string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        state.History.Add(new ConversationTurn(role, text.Trim(), clock.UtcNow));
    }

    private static string? NormalizeOverride(string? overrideText)
    {
        if (string.IsNullOrWhiteSpace(overrideText))
            return null;

        return overrideText.Trim();
    }

    private (ConversationState state, bool leadJustCreated) EnsureLead(ConversationState state, string normalizedText)
    {
        if (state.LeadId.HasValue || string.IsNullOrWhiteSpace(normalizedText))
            return (state, false);

        var leadId = Guid.NewGuid();
        return (state with { LeadId = leadId, LeadCreatedAtUtc = clock.UtcNow }, true);
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
    Dictionary<string, string> Collected,
    IReadOnlyList<ConversationTurn> History,
    Guid? LeadId,
    DateTimeOffset? LeadCreatedAtUtc,
    bool LeadJustCreated);

public sealed record ConversationState(
    string SiteId,
    int StepIndex,
    Dictionary<string, string> Collected,
    DateTimeOffset UpdatedAtUtc,
    List<ConversationTurn> History,
    string? SystemPromptOverride,
    Guid? LeadId,
    DateTimeOffset? LeadCreatedAtUtc);

public sealed record ConversationTurn(
    string Role,
    string Text,
    DateTimeOffset AtUtc);

public sealed record LlmReply(
    [property: JsonPropertyName("reply_text")] string ReplyText,
    [property: JsonPropertyName("collected")] List<LlmCollectedField>? Collected,
    [property: JsonPropertyName("done")] bool Done);

public sealed record LlmCollectedField(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);
