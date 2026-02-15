using System.Linq;

namespace LeadRelay.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct);

    Task SendTemplateAsync(
        string toEmail,
        string? templateAlias,
        int? templateId,
        IReadOnlyDictionary<string, string> templateModel,
        CancellationToken ct)
    {
        var subject = string.IsNullOrWhiteSpace(templateAlias)
            ? "LeadRelay notification"
            : $"LeadRelay template email ({templateAlias})";
        var body = string.Join(Environment.NewLine, templateModel.Select(x => $"{x.Key}: {x.Value}"));
        return SendAsync(toEmail, subject, body, ct);
    }
}
