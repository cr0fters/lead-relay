namespace LeadRelay.Infrastructure.Persistence;

public sealed class CustomerRecord
{
    public Guid Id { get; set; }
    public string SiteId { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ExternalContactId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
