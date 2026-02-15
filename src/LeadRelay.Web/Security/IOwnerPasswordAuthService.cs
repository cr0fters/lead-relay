namespace LeadRelay.Web.Security;

public interface IOwnerPasswordAuthService
{
    Task<OwnerAuthContext?> ValidateCredentialsAsync(string? email, string? password, CancellationToken ct);
    Task RequestPasswordResetAsync(string? email, Func<string, string> resetUrlFactory, string? userAgent, CancellationToken ct);
    Task<bool> ResetPasswordAsync(string? email, string? token, string? newPassword, CancellationToken ct);
}
