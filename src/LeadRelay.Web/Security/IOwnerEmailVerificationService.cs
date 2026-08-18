namespace LeadRelay.Web.Security;

public interface IOwnerEmailVerificationService
{
    Task<bool> IsVerifiedAsync(string siteId, CancellationToken ct);
    Task<bool> RequestAsync(string siteId, Func<string, string> verificationUrlFactory, CancellationToken ct);
    Task<bool> VerifyAsync(string? email, string? token, CancellationToken ct);
}
