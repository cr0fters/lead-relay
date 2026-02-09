namespace LeadRelay.Domain.Projects;

public sealed class Project
{
    public required Guid Id { get; init; }
    public required string SiteId { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Name { get; set; } = "";
    public string? Summary { get; set; }
    public string Status { get; set; } = ProjectStatuses.New;
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
