namespace LeadRelay.Infrastructure.Persistence;

public sealed class OwnerAccountRecord
{
    public string SiteId { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? ResetTokenHash { get; set; }
    public DateTimeOffset? ResetTokenExpiresAtUtc { get; set; }
    public string? EmailVerificationTokenHash { get; set; }
    public DateTimeOffset? EmailVerificationTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? EmailVerificationSentAtUtc { get; set; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }
    public DateTimeOffset? WidgetInstalledAtUtc { get; set; }
    public string? WidgetInstalledDomain { get; set; }
    public DateTimeOffset? LegalDocumentsAcceptedAtUtc { get; set; }
    public string? TermsVersion { get; set; }
    public string? PrivacyPolicyVersion { get; set; }
    public long SessionVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
