using LeadRelay.Domain.Sites;

namespace LeadRelay.Web.Controllers;

internal static class SiteValidationExtensions
{
    public static bool IsValid(this Site site, out string error)
    {
        if (string.IsNullOrWhiteSpace(site.Id))
        {
            error = "Site id is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(site.Name))
        {
            error = "Site name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(site.OwnerEmail))
        {
            error = "Owner email is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(site.WhatsAppNumber))
        {
            error = "WhatsApp number is required.";
            return false;
        }

        if (site.Fields.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name)))
        {
            error = "All fields must include an id and name.";
            return false;
        }

        if (site.Fields
            .Select(x => x.Id.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != site.Fields.Count)
        {
            error = "Field ids must be unique.";
            return false;
        }

        error = "";
        return true;
    }
}
