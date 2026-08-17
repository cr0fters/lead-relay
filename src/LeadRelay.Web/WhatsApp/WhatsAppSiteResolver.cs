using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppSiteResolver(
    ISiteRepository sites,
    ILogger<WhatsAppSiteResolver> logger)
{
    public async Task<Site?> ResolveAsync(
        string? phoneNumberId,
        string? displayPhoneNumber,
        CancellationToken ct)
    {
        var normalizedPhoneNumberId = (phoneNumberId ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPhoneNumberId))
        {
            var byPhoneId = await sites.GetByWhatsAppPhoneNumberIdAsync(normalizedPhoneNumberId, ct);
            if (byPhoneId is not null)
                return byPhoneId;

            logger.LogWarning("No site matched incoming WhatsApp phone_number_id {PhoneNumberId}; message quarantined.", normalizedPhoneNumberId);
            return null;
        }

        var normalizedDisplayNumber = NormalizeDigits(displayPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedDisplayNumber))
        {
            logger.LogWarning("Incoming WhatsApp message had no sender identity and was quarantined.");
            return null;
        }

        var allSites = await sites.GetAllAsync(ct);
        var displayMatches = allSites
            .Where(x => NormalizeDigits(x.WhatsAppNumber) == normalizedDisplayNumber)
            .Take(2)
            .ToList();
        if (displayMatches.Count == 1)
            return displayMatches[0];

        if (displayMatches.Count > 1)
        {
            logger.LogError(
                "Incoming WhatsApp display number {DisplayNumber} matched multiple sites and was quarantined.",
                normalizedDisplayNumber);
            return null;
        }

        logger.LogWarning(
            "Incoming WhatsApp message could not be attributed. DisplayNumber={DisplayNumber}.",
            normalizedDisplayNumber);
        return null;
    }

    private static string? NormalizeDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
