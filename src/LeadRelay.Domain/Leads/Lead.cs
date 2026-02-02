namespace LeadRelay.Domain.Leads;

public sealed class Lead
{
    public required Guid Id { get; init; }
    public required string SiteId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Intent { get; set; }
    public string? Notes { get; set; }
    public string? PageUrl { get; set; }
    public string? Referrer { get; set; }
    public Dictionary<string, string> Utm { get; init; } = new();
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
