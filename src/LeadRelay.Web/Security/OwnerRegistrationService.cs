using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Fields;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Net.Mail;

namespace LeadRelay.Web.Security;

public sealed class OwnerRegistrationService(
    LeadRelayDbContext db,
    IClock clock,
    ILogger<OwnerRegistrationService> logger) : IOwnerRegistrationService
{
    private readonly PasswordHasher<OwnerAccountRecord> _hasher = new();

    public async Task<OwnerRegistrationResult> RegisterAsync(OwnerRegistrationRequest request, CancellationToken ct)
    {
        var normalizedSiteName = (request.SiteName ?? "").Trim();
        var normalizedEmail = NormalizeEmail(request.OwnerEmail);
        var normalized = ConversationFieldNormalizer.Normalize(request.Fields);
        if (normalized.Error is not null)
            return OwnerRegistrationResult.Failure(normalized.Error);

        var normalizedFields = normalized.Fields;

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
            return OwnerRegistrationResult.Failure("Business name is required.");

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return OwnerRegistrationResult.Failure("Email is required.");

        if (!MailAddress.TryCreate(normalizedEmail, out _))
            return OwnerRegistrationResult.Failure("Enter a valid email address.");

        if (!IsAcceptablePassword(request.Password))
            return OwnerRegistrationResult.Failure("Password must be at least 8 characters.");

        var existingSite = await db.Sites
            .AsNoTracking()
            .AnyAsync(x => x.OwnerEmail.ToLower() == normalizedEmail, ct);
        if (existingSite)
            return OwnerRegistrationResult.Failure("An account with that email already exists.");

        var siteId = Guid.NewGuid().ToString("D");
        var site = new SiteRecord
        {
            Id = siteId,
            Name = normalizedSiteName,
            BusinessSummary = string.IsNullOrWhiteSpace(request.BusinessSummary) ? null : request.BusinessSummary.Trim(),
            Fields = normalizedFields,
            OwnerEmail = normalizedEmail,
            WhatsAppNumber = ""
        };

        var account = new OwnerAccountRecord
        {
            SiteId = siteId,
            UpdatedAtUtc = clock.UtcNow
        };
        account.PasswordHash = _hasher.HashPassword(account, request.Password!);

        db.Sites.Add(site);
        db.OwnerAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsOwnerEmailConflict(exception))
        {
            return OwnerRegistrationResult.Failure("An account with that email already exists.");
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Owner registration database write failed for site {SiteId}.", siteId);
            return OwnerRegistrationResult.Failure("The account could not be created right now. Please try again.");
        }

        return OwnerRegistrationResult.Success(new OwnerAuthContext(siteId, normalizedEmail));
    }

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsOwnerEmailConflict(DbUpdateException exception)
        => exception.InnerException is MySqlException mysqlException &&
           mysqlException.ErrorCode == MySqlErrorCode.DuplicateKeyEntry &&
           mysqlException.Message.Contains("IX_Sites_OwnerEmail", StringComparison.OrdinalIgnoreCase);

    private static bool IsAcceptablePassword(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= 8;
    }
}
