using System.Globalization;
using System.Text;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;

namespace LeadRelay.Web.Leads;

public static class LeadCsvExporter
{
    public static string Export(
        IReadOnlyList<LeadExportRow> rows,
        IReadOnlyList<ConversationField> configuredFields)
    {
        var fields = BuildFieldColumns(rows, configuredFields);
        var output = new StringBuilder();
        WriteRow(output,
        [
            "Lead ID",
            "Created at UTC",
            "Name",
            "Email",
            "Phone",
            "Channel",
            "Stage",
            "Project summary",
            "Private notes",
            "Next action",
            "Next action due UTC",
            .. fields.Select(x => x.Label)
        ]);

        foreach (var row in rows)
        {
            WriteRow(output,
            [
                row.Id.ToString("D"),
                row.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                row.Name ?? "",
                row.Email ?? "",
                row.Phone ?? "",
                row.Channel,
                row.ProjectStage,
                row.ProjectSummary ?? "",
                row.OwnerNotes ?? "",
                row.NextAction ?? "",
                row.NextActionAtUtc?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                .. fields.Select(field => row.Fields.TryGetValue(field.Id, out var value) ? value : "")
            ]);
        }

        return output.ToString();
    }

    private static IReadOnlyList<FieldColumn> BuildFieldColumns(
        IReadOnlyList<LeadExportRow> rows,
        IReadOnlyList<ConversationField> configuredFields)
    {
        var columns = new List<FieldColumn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in configuredFields)
        {
            var id = (field.Id ?? "").Trim();
            if (string.IsNullOrWhiteSpace(id) ||
                string.Equals(id, "project_summary", StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(id))
                continue;
            var label = string.IsNullOrWhiteSpace(field.Name) ? id : field.Name.Trim();
            columns.Add(new FieldColumn(id, label));
        }

        foreach (var id in rows.SelectMany(x => x.Fields.Keys).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var normalized = (id ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.Equals(normalized, "project_summary", StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(normalized))
                continue;
            columns.Add(new FieldColumn(normalized, normalized));
        }

        return columns;
    }

    private static void WriteRow(StringBuilder output, IEnumerable<string> values)
    {
        output.AppendLine(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string? value)
    {
        var safe = PreventFormulaExecution(value ?? "");
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private static string PreventFormulaExecution(string value)
    {
        var candidate = value.TrimStart(' ', '\t', '\r', '\n');
        var startsWithControlCharacter = value.Length > 0 && value[0] is '\t' or '\r' or '\n';
        return startsWithControlCharacter ||
               (candidate.Length > 0 && candidate[0] is '=' or '+' or '-' or '@')
            ? $"'{value}"
            : value;
    }

    private sealed record FieldColumn(string Id, string Label);
}
