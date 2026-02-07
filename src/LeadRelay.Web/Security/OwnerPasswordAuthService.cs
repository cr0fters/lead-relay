using System.Security.Cryptography;
using System.Text;
using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Security;

public sealed class OwnerPasswordAuthService(
    LeadRelayDbContext db,
    IClock clock,
    IEmailSender emailSender,
    IOptions<OwnerPortalOptions> options) : IOwnerPasswordAuthService
{
    private readonly OwnerPortalOptions _options = options.Value;
    private readonly PasswordHasher<OwnerAccountRecord> _hasher = new();

    public async Task<OwnerAuthContext?> ValidateCredentialsAsync(string? email, string? password, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password)) return null;

        var site = await FindSiteByOwnerEmailAsync(normalizedEmail, ct);
        if (site is null) return null;

        var account = await db.OwnerAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == site.Id, ct);
        if (account is null || string.IsNullOrWhiteSpace(account.PasswordHash)) return null;

        var result = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        return new OwnerAuthContext(site.Id, site.OwnerEmail);
    }

    public async Task RequestPasswordResetAsync(string? email, Func<string, string> resetUrlFactory, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail)) return;

        var site = await FindSiteByOwnerEmailAsync(normalizedEmail, ct);
        if (site is null) return;

        var account = await db.OwnerAccounts.FirstOrDefaultAsync(x => x.SiteId == site.Id, ct);
        if (account is null)
        {
            account = new OwnerAccountRecord
            {
                SiteId = site.Id,
                UpdatedAtUtc = clock.UtcNow
            };
            db.OwnerAccounts.Add(account);
        }

        var rawToken = CreateToken();
        account.ResetTokenHash = HashToken(rawToken);
        account.ResetTokenExpiresAtUtc = clock.UtcNow.AddMinutes(Math.Clamp(_options.PasswordResetTtlMinutes, 10, 180));
        account.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(ct);

        var resetUrl = resetUrlFactory(rawToken);
        var body = $"""
                    We received a password reset request for your LeadRelay owner portal.

                    Reset your password: {resetUrl}

                    This link expires in {_options.PasswordResetTtlMinutes} minutes.
                    If you did not request this, you can ignore this email.
                    """;
        await emailSender.SendAsync(site.OwnerEmail, "Reset your LeadRelay owner password", body, ct);
    }

    public async Task<bool> ResetPasswordAsync(string? email, string? token, string? newPassword, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedToken = (token ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedToken)) return false;
        if (!IsAcceptablePassword(newPassword)) return false;

        var site = await FindSiteByOwnerEmailAsync(normalizedEmail, ct);
        if (site is null) return false;

        var account = await db.OwnerAccounts.FirstOrDefaultAsync(x => x.SiteId == site.Id, ct);
        if (account is null || string.IsNullOrWhiteSpace(account.ResetTokenHash) || account.ResetTokenExpiresAtUtc is null)
            return false;

        if (account.ResetTokenExpiresAtUtc.Value < clock.UtcNow) return false;
        if (!FixedTimeEquals(account.ResetTokenHash, HashToken(normalizedToken))) return false;

        account.PasswordHash = _hasher.HashPassword(account, newPassword!);
        account.ResetTokenHash = null;
        account.ResetTokenExpiresAtUtc = null;
        account.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<SiteRecord?> FindSiteByOwnerEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        return await db.Sites
            .FirstOrDefaultAsync(x => x.OwnerEmail.ToLower() == normalizedEmail, ct);
    }

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsAcceptablePassword(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= 8;
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url(bytes.ToArray());
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Base64Url(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
