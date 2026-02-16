using System.Text;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Web.Fields;

internal static class ConversationFieldNormalizer
{
    public static (List<ConversationField> Fields, string? Error) Normalize(IEnumerable<ConversationField>? fields)
    {
        var normalized = new List<ConversationField>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in fields ?? [])
        {
            var rawId = (entry.Id ?? "").Trim();
            var name = (entry.Name ?? "").Trim();
            var description = (entry.Description ?? "").Trim();

            if (string.IsNullOrWhiteSpace(rawId) &&
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(description))
                continue;

            if (string.IsNullOrWhiteSpace(name))
                return (normalized, "Each field needs a name.");

            var id = string.IsNullOrWhiteSpace(rawId) ? Slugify(name) : rawId;
            if (string.IsNullOrWhiteSpace(id))
                return (normalized, "Each field needs a valid name.");

            if (!seen.Add(id))
                return (normalized, "Field ids must be unique.");

            normalized.Add(new ConversationField
            {
                Id = id,
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description
            });
        }

        return (normalized, null);
    }

    private static string Slugify(string value)
    {
        var input = (value ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(input)) return "";

        var builder = new StringBuilder(input.Length);
        var lastWasSeparator = false;
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '.')
            {
                if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }
        }

        return builder.ToString().Trim('_');
    }
}
