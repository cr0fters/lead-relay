using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppConversationService(
    IClock clock,
    LeadRelayDbContext db,
    OpenAIClient openAi,
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ConversationOptions> conversationOptions,
    ILogger<WhatsAppConversationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ConversationReply> HandleMessageAsync(
        Site site,
        string waId,
        string? text,
        string? contactName,
        string? systemPromptOverride,
        CancellationToken ct)
    {
        if (!conversationOptions.Value.BotEnabled)
        {
            return new ConversationReply(
                "",
                false,
                new Dictionary<string, string>(),
                Array.Empty<ConversationTurn>(),
                null,
                null,
                false,
                Array.Empty<string>());
        }

        var normalizedText = (text ?? "").Trim();
        var normalizedContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        var state = await LoadStateAsync(site.Id, waId, ct);
        if (state is not null && IsSessionExpired(state))
        {
            state = new ConversationState(
                site.Id,
                waId,
                0,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                clock.UtcNow,
                clock.UtcNow,
                clock.UtcNow,
                state.IsPaused,
                normalizedContactName ?? state.ContactName,
                new List<ConversationTurn>(),
                NormalizeOverride(systemPromptOverride),
                null,
                null);
        }
        if (state is null)
        {
            state = new ConversationState(
                site.Id,
                waId,
                0,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                clock.UtcNow,
                clock.UtcNow,
                clock.UtcNow,
                false,
                normalizedContactName,
                new List<ConversationTurn>(),
                NormalizeOverride(systemPromptOverride),
                null,
                null);

            if (!string.IsNullOrWhiteSpace(normalizedText))
                AppendTurn(state, "user", normalizedText);

            if (state.IsPaused)
            {
                await SaveStateAsync(state, ct);
                return new ConversationReply(
                    "",
                    false,
                    state.Collected,
                    state.History.ToList(),
                    state.LeadId,
                    state.LeadCreatedAtUtc,
                    false,
                    Array.Empty<string>());
            }

            var leadJustCreated = false;
            if (conversationOptions.Value.SubmitLeadOnFirstMessage)
            {
                (state, leadJustCreated) = EnsureLead(state, normalizedText);
            }

            var firstPrompt = GetPrompt(site, state.StepIndex);
            var intro = BuildIntro(site);
            AppendTurn(state, "assistant", intro);
            var replies = new List<string> { intro };
            if (!string.IsNullOrWhiteSpace(firstPrompt))
            {
                AppendTurn(state, "assistant", firstPrompt);
                replies.Add(firstPrompt);
            }
            await SaveStateAsync(state, ct);
            return new ConversationReply(
                intro,
                false,
                new Dictionary<string, string>(),
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated,
                replies);
        }

        AppendTurn(state, "user", normalizedText);
        state = state with { LastActivityAtUtc = clock.UtcNow, UpdatedAtUtc = clock.UtcNow };
        if (!string.IsNullOrWhiteSpace(normalizedContactName) &&
            !string.Equals(state.ContactName, normalizedContactName, StringComparison.Ordinal))
        {
            state = state with { ContactName = normalizedContactName, UpdatedAtUtc = clock.UtcNow };
        }

        if (state.IsPaused)
        {
            await SaveStateAsync(state, ct);
            return new ConversationReply(
                "",
                false,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                false,
                Array.Empty<string>());
        }

        var normalizedOverride = NormalizeOverride(systemPromptOverride);
        if (!string.IsNullOrWhiteSpace(normalizedOverride) &&
            !string.Equals(state.SystemPromptOverride, normalizedOverride, StringComparison.Ordinal))
        {
            state = state with { SystemPromptOverride = normalizedOverride, UpdatedAtUtc = clock.UtcNow };
        }

        var leadJustCreatedExisting = false;
        if (conversationOptions.Value.SubmitLeadOnFirstMessage)
        {
            (state, leadJustCreatedExisting) = EnsureLead(state, normalizedText);
        }

        if (conversationOptions.Value.UseLlm)
        {
            var llmReply = await TryHandleWithLlmAsync(site, state, normalizedText, ct);
            if (llmReply is not null)
                return llmReply with
                {
                    History = state.History.ToList(),
                    LeadId = state.LeadId,
                    LeadCreatedAtUtc = state.LeadCreatedAtUtc,
                    LeadJustCreated = leadJustCreatedExisting,
                    Replies = new[] { llmReply.ReplyText }
                };
        }

        return await HandleDeterministicAsync(site, state, normalizedText, leadJustCreatedExisting, ct);
    }

    private async Task<ConversationReply> HandleDeterministicAsync(
        Site site,
        ConversationState state,
        string normalizedText,
        bool leadJustCreated,
        CancellationToken ct)
    {
        var field = GetField(site, state.StepIndex);
        if (field is null)
        {
            var completedReply = "Thanks! We’ll be in touch shortly.";
            AppendTurn(state, "assistant", completedReply);
            await SaveStateAsync(state, ct);
            return new ConversationReply(
                completedReply,
                true,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated,
                new[] { completedReply });
        }

        if (!TryAcceptField(field, normalizedText, out var value, out var errorReply))
        {
            AppendTurn(state, "assistant", errorReply);
            await SaveStateAsync(state, ct);
            return new ConversationReply(
                errorReply,
                false,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated,
                new[] { errorReply });
        }

        state.Collected[field.Key] = value;
        state = state with { StepIndex = state.StepIndex + 1, UpdatedAtUtc = clock.UtcNow };

        var nextPrompt = GetPrompt(site, state.StepIndex);
        if (nextPrompt is null)
        {
            var completedReply = "Thanks! We’ll be in touch shortly.";
            AppendTurn(state, "assistant", completedReply);
            await SaveStateAsync(state, ct);
            return new ConversationReply(
                completedReply,
                true,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated,
                new[] { completedReply });
        }

        AppendTurn(state, "assistant", nextPrompt);
        await SaveStateAsync(state, ct);
        return new ConversationReply(
            nextPrompt,
            false,
            state.Collected,
            state.History.ToList(),
            state.LeadId,
            state.LeadCreatedAtUtc,
            leadJustCreated,
            new[] { nextPrompt });
    }

    private async Task<ConversationReply?> TryHandleWithLlmAsync(
        Site site,
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
                            done = new { type = "boolean" },
                            project_summary = new { type = new[] { "string", "null" } }
                        },
                        required = new[] { "reply_text", "collected", "done", "project_summary" }
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
        merged = UpdateProjectSummary(merged, reply.ProjectSummary, state, normalizedText);
        var requiredComplete = AreRequiredFieldsFilled(site, merged);
        var done = reply.Done || (requiredComplete && site.OptionalFields.Count == 0);
        var replyText = reply.ReplyText.Trim();

        AppendTurn(state, "assistant", replyText);

        state = state with { Collected = merged, UpdatedAtUtc = clock.UtcNow };
        await SaveStateAsync(state, ct);

        if (done)
        {
            return new ConversationReply(
                replyText,
                true,
                merged,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                false,
                new[] { replyText });
        }

        var nextIndex = GetNextStepIndex(site, merged);
        state = state with { StepIndex = nextIndex, UpdatedAtUtc = clock.UtcNow };
        await SaveStateAsync(state, ct);

        return new ConversationReply(
            replyText,
            false,
            merged,
            state.History.ToList(),
            state.LeadId,
            state.LeadCreatedAtUtc,
            false,
            new[] { replyText });
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
        if (!string.IsNullOrWhiteSpace(state.ContactName))
            sb.AppendLine($"Contact name: {state.ContactName}");

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

        sb.AppendLine();
        sb.AppendLine("Project summary instructions:");
        sb.AppendLine("- Maintain a 1-2 sentence summary based on all user messages so far.");
        sb.AppendLine("- If a summary already exists, refine it with new details instead of replacing it.");
        sb.AppendLine("- Return the updated summary in project_summary.");

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

        if (!ShouldUseForFieldInference(state, normalizedText))
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

    private static Dictionary<string, string> UpdateProjectSummary(
        Dictionary<string, string> collected,
        string? summary,
        ConversationState state,
        string normalizedText)
    {
        var resolved = string.IsNullOrWhiteSpace(summary)
            ? BuildSummaryFallback(collected, normalizedText)
            : summary.Trim();

        if (string.IsNullOrWhiteSpace(resolved))
            return collected;

        var updated = new Dictionary<string, string>(collected, StringComparer.OrdinalIgnoreCase)
        {
            ["project_summary"] = resolved
        };

        return updated;
    }

    private static string? BuildSummaryFallback(Dictionary<string, string> collected, string normalizedText)
    {
        if (IsGreetingOnly(normalizedText) || normalizedText.Length < 6)
            return collected.TryGetValue("project_summary", out var existingSummary) ? existingSummary : null;

        if (collected.TryGetValue("project_summary", out var existing) && !string.IsNullOrWhiteSpace(existing))
            return $"{existing} {normalizedText}".Trim();

        if (collected.TryGetValue("project_description", out var description) && !string.IsNullOrWhiteSpace(description))
            return $"{description} {normalizedText}".Trim();

        return normalizedText;
    }

    private static bool ShouldUseForFieldInference(ConversationState state, string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
            return false;

        if (state.StepIndex == 0 && state.History.Count <= 1)
            return false;

        if (IsGreetingOnly(normalizedText))
            return false;

        return normalizedText.Length >= 6;
    }

    private static bool IsGreetingOnly(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return normalized is "hi" or "hello" or "hey" or "yo" or "hiya" or "sup";
    }

    private bool IsSessionExpired(ConversationState state)
    {
        var timeout = TimeSpan.FromHours(Math.Max(1, conversationOptions.Value.SessionTimeoutHours));
        return clock.UtcNow - state.LastActivityAtUtc > timeout;
    }

    private async Task<ConversationState?> LoadStateAsync(string siteId, string waId, CancellationToken ct)
    {
        var record = await db.ConversationStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.WaId == waId, ct);
        if (record is null) return null;

        var sessionStarted = record.SessionStartedAtUtc ?? record.UpdatedAtUtc;
        var lastActivity = record.LastActivityAtUtc ?? record.UpdatedAtUtc;
        return new ConversationState(
            record.SiteId,
            record.WaId,
            record.StepIndex,
            record.Collected,
            record.UpdatedAtUtc,
            sessionStarted,
            lastActivity,
            record.IsPaused,
            record.ContactName,
            record.History.Select(x => new ConversationTurn(x.Role, x.Text, x.AtUtc)).ToList(),
            record.SystemPromptOverride,
            record.LeadId,
            record.LeadCreatedAtUtc);
    }

    private async Task SaveStateAsync(ConversationState state, CancellationToken ct)
    {
        var record = await db.ConversationStates
            .FirstOrDefaultAsync(x => x.SiteId == state.SiteId && x.WaId == state.WaId, ct);

        if (record is null)
        {
            record = new ConversationStateRecord
            {
                Id = $"{state.SiteId}:{state.WaId}",
                SiteId = state.SiteId,
                WaId = state.WaId
            };
            db.ConversationStates.Add(record);
        }

        record.StepIndex = state.StepIndex;
        record.Collected = state.Collected;
        record.UpdatedAtUtc = state.UpdatedAtUtc;
        record.SessionStartedAtUtc = state.SessionStartedAtUtc;
        record.LastActivityAtUtc = state.LastActivityAtUtc;
        record.IsPaused = state.IsPaused;
        record.ContactName = state.ContactName;
        record.History = state.History.Select(x => new ConversationTurnRecord(x.Role, x.Text, x.AtUtc)).ToList();
        record.SystemPromptOverride = state.SystemPromptOverride;
        record.LeadId = state.LeadId;
        record.LeadCreatedAtUtc = state.LeadCreatedAtUtc;

        await db.SaveChangesAsync(ct);
    }

    public async Task SetPausedAsync(string siteId, string waId, bool paused, CancellationToken ct)
    {
        var state = await LoadStateAsync(siteId, waId, ct)
                    ?? new ConversationState(
                        siteId,
                        waId,
                        0,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        clock.UtcNow,
                        clock.UtcNow,
                        clock.UtcNow,
                        paused,
                        null,
                        new List<ConversationTurn>(),
                        null,
                        null,
                        null);

        state = state with { IsPaused = paused, UpdatedAtUtc = clock.UtcNow };
        await SaveStateAsync(state, ct);
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

    private static string BuildIntro(Site site)
    {
        return BuildIntroMessage(site);
    }

    private static string BuildIntroMessage(Site site)
    {
        var intro = string.IsNullOrWhiteSpace(site.IntroMessage)
            ? "Thanks for reaching out! I’m an AI assistant helping the team respond quickly. Someone on the team will follow up shortly."
            : site.IntroMessage;

        intro = intro.Replace("{site_name}", site.Name, StringComparison.OrdinalIgnoreCase);

        return intro.Trim();
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
    bool LeadJustCreated,
    IReadOnlyList<string> Replies);

public sealed record ConversationState(
    string SiteId,
    string WaId,
    int StepIndex,
    Dictionary<string, string> Collected,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset SessionStartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    bool IsPaused,
    string? ContactName,
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
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("project_summary")] string? ProjectSummary);

public sealed record LlmCollectedField(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);
