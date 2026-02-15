namespace LeadRelay.Infrastructure.Email;

public sealed class PostmarkOptions
{
    public bool Enabled { get; set; } = true;
    public string ServerToken { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string? FromName { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.postmarkapp.com";
    public string? PasswordResetTemplateAlias { get; set; }
    public int? PasswordResetTemplateId { get; set; }
}
