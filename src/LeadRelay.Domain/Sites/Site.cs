namespace LeadRelay.Domain.Sites;

public sealed class Site
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string PublicKey { get; init; }
    public required string OwnerEmail { get; init; }
    public required string WhatsAppNumber { get; init; }
}
