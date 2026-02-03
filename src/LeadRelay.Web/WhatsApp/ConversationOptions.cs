namespace LeadRelay.Web.WhatsApp;

public sealed record ConversationOptions
{
    public string GlobalPromptTemplate { get; init; } = ConversationPrompts.DefaultSystemPrompt;
    public bool UseLlm { get; init; } = true;
    public int MaxHistoryTurns { get; init; } = 8;
    public bool SubmitLeadOnFirstMessage { get; init; } = true;
    public int SessionTimeoutHours { get; init; } = 48;
    public bool BotEnabled { get; init; } = true;
}
