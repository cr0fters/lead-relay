using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Leads;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadCsvExporterTests
{
    [Test]
    public void export_includes_configured_and_historical_fields_in_stable_order()
    {
        var rows = new[]
        {
            BuildRow(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["budget"] = "£25,000",
                ["legacy_field"] = "Legacy answer"
            })
        };
        var fields = new[]
        {
            new ConversationField { Id = "budget", Name = "Budget" }
        };

        var csv = LeadCsvExporter.Export(rows, fields);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[0], Does.EndWith("\"Budget\",\"legacy_field\""));
        Assert.That(lines[1], Does.EndWith("\"£25,000\",\"Legacy answer\""));
        Assert.That(lines[1], Does.Not.Contain("conversation"));
    }

    [TestCase("=HYPERLINK(\"https://example.test\")")]
    [TestCase("+1+1")]
    [TestCase("  @SUM(1,1)")]
    [TestCase("\tSUM(1,1)")]
    public void export_prefixes_values_that_spreadsheets_could_execute(string dangerousValue)
    {
        var row = BuildRow(new Dictionary<string, string> { ["answer"] = dangerousValue });

        var csv = LeadCsvExporter.Export([row], []);

        var escaped = dangerousValue.Replace("\"", "\"\"");
        Assert.That(csv, Does.Contain($"\"'{escaped}\""));
    }

    private static LeadExportRow BuildRow(IReadOnlyDictionary<string, string> fields)
    {
        return new LeadExportRow(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero),
            "Jane Example",
            "jane@example.com",
            "447000000000",
            "whatsapp",
            "qualified",
            "Kitchen renovation",
            "Prefers email",
            "Send proposal",
            new DateTimeOffset(2026, 8, 20, 9, 30, 0, TimeSpan.Zero),
            fields);
    }
}
