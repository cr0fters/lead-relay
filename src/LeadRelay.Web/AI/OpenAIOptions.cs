namespace LeadRelay.Web.AI;

public sealed record OpenAIOptions
{
    public bool Enabled { get; init; } = true;
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gpt-4o-mini";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public int MaxOutputTokens { get; init; } = 400;
    public double Temperature { get; init; } = 0.4;
}
