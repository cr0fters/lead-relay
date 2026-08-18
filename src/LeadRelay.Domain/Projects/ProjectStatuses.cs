namespace LeadRelay.Domain.Projects;

public static class ProjectStatuses
{
    public const string New = "new";
    public const string Qualified = "qualified";
    public const string Contacted = "contacted";
    public const string Won = "won";
    public const string Lost = "lost";

    public static IReadOnlyList<string> OwnerStages { get; } = Array.AsReadOnly(
    [
        New,
        Qualified,
        Contacted,
        Won,
        Lost
    ]);

    public static bool IsOwnerStage(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return OwnerStages.Contains(normalized, StringComparer.Ordinal);
    }

    public static string Normalize(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return IsOwnerStage(normalized) ? normalized : New;
    }
}
