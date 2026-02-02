using LeadRelay.Application.Abstractions;

namespace LeadRelay.Infrastructure.Email;

public sealed class ConsoleEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct)
    {
        Console.WriteLine("=== EMAIL ===");
        Console.WriteLine($"To: {toEmail}");
        Console.WriteLine($"Subject: {subject}");
        Console.WriteLine(bodyText);
        Console.WriteLine("=============");
        return Task.CompletedTask;
    }
}
