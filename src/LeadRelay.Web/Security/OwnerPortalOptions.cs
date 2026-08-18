namespace LeadRelay.Web.Security;

public sealed class OwnerPortalOptions
{
    public string SigningSecret { get; init; } = "";
    public string? PreviousSigningSecret { get; init; }
    public string SessionCookieName { get; init; } = "leadrelay_owner_session";
    public int SessionTtlHours { get; init; } = 12;
    public int PasswordResetTtlMinutes { get; init; } = 30;
    public int EmailVerificationTtlHours { get; init; } = 24;
    public int EmailVerificationResendCooldownSeconds { get; init; } = 60;
}
