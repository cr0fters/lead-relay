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

        if (site.Fields.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Prompt)))
        {
            error = "All required fields must include a key and prompt.";
            return false;
        }

        if (site.OptionalFields.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Prompt)))
        {
            error = "All optional fields must include a key and prompt.";
            return false;
        }

        error = "";
        return true;
    }
}
