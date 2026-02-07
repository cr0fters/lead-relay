namespace LeadRelay.Infrastructure.Persistence;

public sealed class OwnerAccountRecord
{
    public string SiteId { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? ResetTokenHash { get; set; }
    public DateTimeOffset? ResetTokenExpiresAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
