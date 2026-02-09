namespace LeadRelay.Domain.Customers;

public sealed class Customer
{
    public required Guid Id { get; init; }
    public required string SiteId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ExternalContactId { get; set; }
}
