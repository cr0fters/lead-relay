using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Leads;
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
        string contactId,
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
        var normalizedContactId = (contactId ?? "").Trim();
        var normalizedContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        var state = new ConversationState(
            site.Id,
            normalizedContactId,
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

        var leadJustCreated = false;
        if (conversationOptions.Value.SubmitLeadOnFirstMessage)
            (state, leadJustCreated) = await EnsureLeadAsync(state, normalizedText, ct);

        state = await ReconcileStateWithProjectAsync(site, state, ct);

        if (!string.IsNullOrWhiteSpace(normalizedText))
            AppendTurn(state, "user", normalizedText);

        if (state.IsPaused)
        {
            return new ConversationReply(
                "",
                false,
                state.Collected,
                state.History.ToList(),
                state.LeadId,
                state.LeadCreatedAtUtc,
                leadJustCreated,
                Array.Empty<string>());
        }

        if (!state.LeadId.HasValue)
        {
            var firstPrompt = GetPrompt(site, state.StepIndex);
            var intro = BuildIntro(site);
            AppendTurn(state, "assistant", intro);
            var replies = new List<string> { intro };
            if (!string.IsNullOrWhiteSpace(firstPrompt))
            {
                AppendTurn(state, "assistant", firstPrompt);
                replies.Add(firstPrompt);
            }

            return new ConversationReply(
                intro,
                false,
                state.Collected,
                state.History.ToList(),
                null,
                null,
                leadJustCreated,
                replies);
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
                    LeadJustCreated = leadJustCreated,
                    Replies = new[] { llmReply.ReplyText }
                };
        }

        return HandleDeterministic(site, state, normalizedText, leadJustCreated);
    }

    private ConversationReply HandleDeterministic(
        Site site,
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
                leadJustCreated,
                new[] { completedReply });
        }

        if (!TryAcceptField(normalizedText, out var value))
        {
            var errorReply = BuildPrompt(field);
            AppendTurn(state, "assistant", errorReply);
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

        state.Collected[GetFieldId(field)] = value;
        state = state with { StepIndex = state.StepIndex + 1, UpdatedAtUtc = clock.UtcNow };

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
                leadJustCreated,
                new[] { completedReply });
        }

        AppendTurn(state, "assistant", nextPrompt);
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
        var projectSummary = ResolveProjectSummary(reply.ProjectSummary, state, normalizedText, merged);
        var allConfiguredFieldsFilled = AreAllConfiguredFieldsFilled(site, merged);
        var done = reply.Done || allConfiguredFieldsFilled;
        var replyText = reply.ReplyText.Trim();

        AppendTurn(state, "assistant", replyText);
        state.Collected.Clear();
        foreach (var pair in merged)
            state.Collected[pair.Key] = pair.Value;
        state = state with { StepIndex = GetNextStepIndex(site, merged), UpdatedAtUtc = clock.UtcNow };

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
                new[] { replyText },
                projectSummary);
        }

        return new ConversationReply(
            replyText,
            false,
            merged,
            state.History.ToList(),
            state.LeadId,
            state.LeadCreatedAtUtc,
            false,
            new[] { replyText },
            projectSummary);
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
        sb.AppendLine("Configured project fields (id | name | description):");
        foreach (var field in site.Fields)
            sb.AppendLine($"- {GetFieldId(field)} | {GetFieldName(field)} | {GetFieldDescription(field)}");

        sb.AppendLine("Rules:");
        sb.AppendLine("- Be concise, friendly, and conversational.");
        sb.AppendLine("- Ask at most one question per reply.");
        sb.AppendLine("- If the user already provided a field, do not ask again.");
        sb.AppendLine("- If missing configured fields, ask for one missing field.");
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

        foreach (var field in site.Fields)
        {
            if (!TryResolveFieldId(site, proposed, field, out var raw))
                continue;

            if (!TryAcceptField(raw, out var value))
                continue;

            merged[GetFieldId(field)] = value;
        }

        return merged;
    }

    private static int GetNextStepIndex(Site site, Dictionary<string, string> collected)
    {
        for (var i = 0; i < site.Fields.Count; i++)
        {
            if (!collected.ContainsKey(GetFieldId(site.Fields[i])))
                return i;
        }

        return site.Fields.Count;
    }

    private static bool AreAllConfiguredFieldsFilled(Site site, Dictionary<string, string> collected)
    {
        foreach (var field in site.Fields)
        {
            if (!collected.ContainsKey(GetFieldId(field)))
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

        var field = GetField(site, state.StepIndex);
        if (field is null)
            return collected;

        if (!WasCurrentFieldJustPrompted(state, field))
            return collected;

        if (IsGreetingOnly(normalizedText))
            return collected;

        if (collected.ContainsKey(GetFieldId(field)))
            return collected;

        if (!TryAcceptField(normalizedText, out var value))
            return collected;

        var updated = new Dictionary<string, string>(collected, StringComparer.OrdinalIgnoreCase)
        {
            [GetFieldId(field)] = value
        };

        return updated;
    }

    private static bool WasCurrentFieldJustPrompted(ConversationState state, ConversationField field)
    {
        var lastAssistantTurn = state.History
            .LastOrDefault(x => string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastAssistantTurn is null || string.IsNullOrWhiteSpace(lastAssistantTurn.Text))
            return false;

        var lastAssistantText = NormalizeForMatch(lastAssistantTurn.Text);
        var fieldId = NormalizeForMatch(GetFieldId(field));
        if (!string.IsNullOrWhiteSpace(fieldId) && lastAssistantText.Contains(fieldId, StringComparison.Ordinal))
            return true;

        var fieldName = NormalizeForMatch(GetFieldName(field));
        return !string.IsNullOrWhiteSpace(fieldName) && lastAssistantText.Contains(fieldName, StringComparison.Ordinal);
    }

    private static string NormalizeForMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return Regex.Replace(text.Trim().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private static string? ResolveProjectSummary(
        string? summary,
        ConversationState state,
        string normalizedText,
        Dictionary<string, string> collected)
    {
        var resolved = string.IsNullOrWhiteSpace(summary)
            ? BuildSummaryFallback(state, collected, normalizedText)
            : summary.Trim();

        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    private static string? BuildSummaryFallback(
        ConversationState state,
        Dictionary<string, string> collected,
        string normalizedText)
    {
        if (IsGreetingOnly(normalizedText) || normalizedText.Length < 6)
            return null;

        var priorUserText = state.History
            .Where(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Text)
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(priorUserText) &&
            !string.Equals(priorUserText.Trim(), normalizedText.Trim(), StringComparison.OrdinalIgnoreCase))
            return $"{priorUserText.Trim()} {normalizedText}".Trim();

        if (collected.TryGetValue("project_overview", out var description) && !string.IsNullOrWhiteSpace(description))
            return $"{description} {normalizedText}".Trim();

        return normalizedText;
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

    private async Task<(ConversationState state, bool leadJustCreated)> EnsureLeadAsync(
        ConversationState state,
        string normalizedText,
        CancellationToken ct)
    {
        if (state.LeadId.HasValue || string.IsNullOrWhiteSpace(normalizedText))
            return (state, false);

        var reusableLead = await TryGetReusableOpenLeadAsync(state.SiteId, state.ContactId, ct);
        if (reusableLead is not null)
        {
            return (state with
            {
                LeadId = reusableLead.Value.Id,
                LeadCreatedAtUtc = reusableLead.Value.CreatedAtUtc
            }, false);
        }

        // New lead is created by LeadCaptureService after this method returns.
        return (state, true);
    }

    private async Task<(Guid Id, DateTimeOffset CreatedAtUtc)?> TryGetReusableOpenLeadAsync(
        string siteId,
        string contactId,
        CancellationToken ct)
    {
        var normalizedContactId = NormalizePhone(contactId);
        var customer = await db.Customers.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .FirstOrDefaultAsync(x =>
                x.ExternalContactId == contactId ||
                (!string.IsNullOrWhiteSpace(normalizedContactId) && x.Phone == normalizedContactId), ct);
        if (customer is null)
            return null;

        var windowHours = Math.Max(1, conversationOptions.Value.ReuseOpenLeadWindowHours);
        var threshold = clock.UtcNow.AddHours(-windowHours);

        var lead = await db.Leads.AsNoTracking()
            .Where(x =>
                x.SiteId == siteId &&
                x.CustomerId == customer.Id &&
                x.Status == LeadStatuses.Open &&
                x.CreatedAtUtc >= threshold)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        return lead is null ? null : (lead.Id, lead.CreatedAtUtc);
    }

    private async Task<ConversationState> ReconcileStateWithProjectAsync(
        Site site,
        ConversationState state,
        CancellationToken ct)
    {
        if (!state.LeadId.HasValue)
            return state;

        var persisted = await LoadLeadConversationStateAsync(
            site,
            state.LeadId.Value,
            state.ContactId,
            state.ContactName,
            state.SystemPromptOverride,
            ct);
        if (persisted is null)
            return state;

        if (persisted.Collected.Count == 0 && state.Collected.Count == 0)
            return persisted;

        var merged = new Dictionary<string, string>(persisted.Collected, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in state.Collected)
        {
            var key = (pair.Key ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (string.Equals(key, "project_summary", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = (pair.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            merged[key] = value;
        }

        var nextStep = GetNextStepIndex(site, merged);
        if (nextStep == persisted.StepIndex && DictionaryEquals(persisted.Collected, merged))
            return persisted;

        persisted.Collected.Clear();
        foreach (var pair in merged)
            persisted.Collected[pair.Key] = pair.Value;
        return persisted with { StepIndex = nextStep, UpdatedAtUtc = clock.UtcNow };
    }

    private async Task<ConversationState?> LoadLeadConversationStateAsync(
        Site site,
        Guid leadId,
        string contactId,
        string? contactName,
        string? systemPromptOverride,
        CancellationToken ct)
    {
        var row = await (
            from lead in db.Leads.AsNoTracking()
            join project in db.Projects.AsNoTracking()
                on new { lead.SiteId, ProjectId = lead.ProjectId } equals new { project.SiteId, ProjectId = project.Id }
            where lead.SiteId == site.Id && lead.Id == leadId
            select new
            {
                lead.Id,
                lead.SiteId,
                lead.CreatedAtUtc,
                lead.Status,
                lead.IsBotPaused,
                lead.Conversation,
                project.Fields,
                project.Summary
            }).FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in row.Fields)
        {
            var key = (pair.Key ?? "").Trim();
            var value = (pair.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;
            if (string.Equals(key, "project_summary", StringComparison.OrdinalIgnoreCase))
                continue;
            collected[key] = value;
        }

        var history = row.Conversation
            .Select(x => new ConversationTurn(x.Role, x.Text, x.AtUtc))
            .ToList();

        return new ConversationState(
            row.SiteId,
            contactId,
            GetNextStepIndex(site, collected),
            collected,
            clock.UtcNow,
            row.CreatedAtUtc,
            clock.UtcNow,
            row.IsBotPaused,
            contactName,
            history,
            NormalizeOverride(systemPromptOverride),
            row.Id,
            row.CreatedAtUtc);
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue))
                return false;

            if (!string.Equals((pair.Value ?? "").Trim(), (rightValue ?? "").Trim(), StringComparison.Ordinal))
                return false;
        }

        return true;
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
        return field is null ? null : BuildPrompt(field);
    }

    private static bool TryAcceptField(string input, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        value = input.Trim();
        return true;
    }

    private static string NormalizePhone(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : "";
    }

    private static bool TryResolveFieldId(
        Site site,
        IReadOnlyDictionary<string, string> proposed,
        ConversationField field,
        out string value)
    {
        var fieldId = GetFieldId(field);
        if (proposed.TryGetValue(fieldId, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            value = direct.Trim();
            return true;
        }

        var fieldName = GetFieldName(field);
        foreach (var pair in proposed)
        {
            if (!string.Equals(pair.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                value = pair.Value.Trim();
                return true;
            }
        }

        value = "";
        return false;
    }

    private static string BuildPrompt(ConversationField field)
    {
        var name = BuildSentenceFieldName(field);
        var description = GetFieldDescription(field);
        return string.IsNullOrWhiteSpace(description)
            ? $"Could you share your {name}?"
            : $"Could you share your {name}? {description}";
    }

    private static string BuildSentenceFieldName(ConversationField field)
    {
        var name = GetFieldName(field);
        if (string.IsNullOrWhiteSpace(name))
            return "details";

        if (name.Length == 1)
            return name.ToLowerInvariant();

        return char.IsUpper(name[0]) && char.IsLower(name[1])
            ? $"{char.ToLowerInvariant(name[0])}{name[1..]}"
            : name;
    }

    private static string GetFieldId(ConversationField field)
    {
        var id = (field.Id ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return Slugify(field.Name);
    }

    private static string GetFieldName(ConversationField field)
    {
        var name = (field.Name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        return GetFieldId(field);
    }

    private static string GetFieldDescription(ConversationField field)
        => (field.Description ?? "").Trim();

    private static string Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "field";

        var normalized = Regex.Replace(input.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_");
        normalized = normalized.Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "field" : normalized;
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
    IReadOnlyList<string> Replies,
    string? ProjectSummary = null);

public sealed record ConversationState(
    string SiteId,
    string ContactId,
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
