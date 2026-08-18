using LeadRelay.Domain.Sites;
using LeadRelay.Web.Fields;

namespace LeadRelay.Web.Security;

public interface IOwnerRegistrationService
{
    Task<OwnerRegistrationResult> RegisterAsync(OwnerRegistrationRequest request, CancellationToken ct);
}

public sealed record OwnerRegistrationRequest(
    string? SiteName,
    string? BusinessSummary,
    IReadOnlyList<ConversationField>? Fields,
    string? OwnerEmail,
    string? Password,
    bool AcceptedTermsAndPrivacy);

public sealed record OwnerRegistrationField(string Id, string Name, string? Description);

public sealed record OwnerRegistrationPayload(
    string? BusinessSummary,
    IReadOnlyList<OwnerRegistrationField>? Fields);

public static class OwnerRegistrationPayloadParser
{
    public static bool TryNormalizeFields(
        IReadOnlyList<OwnerRegistrationField>? input,
        out List<ConversationField> fields,
        out string? error)
    {
        var mapped = (input ?? [])
            .Select(entry => new ConversationField
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description
            })
            .ToList();

        var result = ConversationFieldNormalizer.Normalize(mapped);
        fields = result.Fields;
        error = result.Error;
        return error is null;
    }
}

public sealed record OwnerRegistrationResult(bool Succeeded, string? Error, OwnerAuthContext? Auth)
{
    public static OwnerRegistrationResult Success(OwnerAuthContext auth) => new(true, null, auth);
    public static OwnerRegistrationResult Failure(string error) => new(false, error, null);
}
