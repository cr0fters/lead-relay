namespace LeadRelay.Infrastructure.Persistence;

public sealed class ProjectRecord
{
    public Guid Id { get; set; }
    public string SiteId { get; set; } = "";
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = "";
    public string? Summary { get; set; }
    public string Status { get; set; } = "new";
    public Dictionary<string, string> Fields { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
