using System.Security.Cryptography;
using System.Text;
using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Security;

public sealed class OwnerEmailVerificationService(
    LeadRelayDbContext db,
    IClock clock,
    IEmailSender emailSender,
    IOptions<OwnerPortalOptions> options) : IOwnerEmailVerificationService
{
    private readonly OwnerPortalOptions _options = options.Value;

    public async Task<bool> IsVerifiedAsync(string siteId, CancellationToken ct)
    {
        var account = await db.OwnerAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        // Operator-managed legacy sites may have no owner account, or a passwordless
        // placeholder created by the forgotten-password flow. Neither is self-service.
        return account is null || string.IsNullOrWhiteSpace(account.PasswordHash) || account.EmailVerifiedAtUtc.HasValue;
    }

    public async Task<bool> RequestAsync(
        string siteId,
        Func<string, string> verificationUrlFactory,
        CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId, ct);
        var account = await db.OwnerAccounts.FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (site is null || account is null || account.EmailVerifiedAtUtc.HasValue)
            return false;

        var cooldown = TimeSpan.FromSeconds(Math.Clamp(_options.EmailVerificationResendCooldownSeconds, 30, 3600));
        if (account.EmailVerificationSentAtUtc.HasValue && account.EmailVerificationSentAtUtc.Value > clock.UtcNow.Subtract(cooldown))
            return false;

        var rawToken = CreateToken();
        account.EmailVerificationTokenHash = HashToken(rawToken);
        account.EmailVerificationTokenExpiresAtUtc = clock.UtcNow.AddHours(Math.Clamp(_options.EmailVerificationTtlHours, 1, 168));
        account.EmailVerificationSentAtUtc = null;
        account.UpdatedAtUtc = clock.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(account).State = EntityState.Detached;
            return false;
        }

        var verificationUrl = verificationUrlFactory(rawToken);
        var body = $"""
                    Welcome to LeadRelay.

                    Verify your email address: {verificationUrl}

                    This link expires in {Math.Clamp(_options.EmailVerificationTtlHours, 1, 168)} hours.
                    If you did not create this account, you can ignore this email.
                    """;
        await emailSender.SendAsync(site.OwnerEmail, "Verify your LeadRelay email", body, ct);

        account.EmailVerificationSentAtUtc = clock.UtcNow;
        account.UpdatedAtUtc = clock.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The email was delivered, but the token may already have been consumed.
            db.Entry(account).State = EntityState.Detached;
        }
        return true;
    }

    public async Task<bool> VerifyAsync(string? email, string? token, CancellationToken ct)
    {
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        var normalizedToken = (token ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedToken))
            return false;

        var account = await db.OwnerAccounts
            .Join(db.Sites.Where(x => x.OwnerEmail.ToLower() == normalizedEmail), account => account.SiteId, site => site.Id, (account, _) => account)
            .FirstOrDefaultAsync(ct);
        if (account is null || account.EmailVerifiedAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(account.EmailVerificationTokenHash) ||
            !account.EmailVerificationTokenExpiresAtUtc.HasValue ||
            account.EmailVerificationTokenExpiresAtUtc.Value <= clock.UtcNow)
            return false;

        if (!FixedTimeEquals(account.EmailVerificationTokenHash, HashToken(normalizedToken)))
            return false;

        account.EmailVerifiedAtUtc = clock.UtcNow;
        account.EmailVerificationTokenHash = null;
        account.EmailVerificationTokenExpiresAtUtc = null;
        account.UpdatedAtUtc = clock.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(account).State = EntityState.Detached;
            return false;
        }
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url(bytes.ToArray());
    }

    private static string HashToken(string rawToken)
        => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static bool FixedTimeEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
