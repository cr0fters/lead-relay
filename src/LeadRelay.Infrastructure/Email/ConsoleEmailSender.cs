using LeadRelay.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LeadRelay.Infrastructure.Email;

public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
    {
        logger.LogInformation("Sending email to {ToEmail} with subject {Subject} and body {BodyText}", toEmail, subject, bodyText);
        return Task.CompletedTask;
    }
}
