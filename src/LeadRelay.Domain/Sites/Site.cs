namespace LeadRelay.Domain.Sites;

public sealed class Site
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> AllowedDomains { get; init; } = Array.Empty<string>();
    public required string OwnerEmail { get; init; }
    public required string WhatsAppNumber { get; init; }
}
