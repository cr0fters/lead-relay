namespace LeadRelay.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct);
}
