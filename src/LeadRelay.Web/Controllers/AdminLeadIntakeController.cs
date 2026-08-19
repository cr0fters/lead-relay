using LeadRelay.Application.Abstractions;
using LeadRelay.Web.Leads;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LeadRelay.Web.Controllers;

[ApiController]
[Route("admin/api/leads")]
public sealed class AdminLeadIntakeController(
    ISiteRepository sites,
    ILeadRepository leads,
    LeadCaptureService leadCapture) : ControllerBase
{
    [HttpPost("intake")]
    [EnableRateLimiting("lead-intake")]
    public async Task<IActionResult> Intake([FromBody] LeadIntakeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SiteId))
            return BadRequest(new { error = "siteId is required." });

        var site = await sites.GetByIdAsync(request.SiteId.Trim(), ct);
        if (site is null) return NotFound(new { error = "Site not found." });

        var existingLead = request.LeadId is null
            ? null
            : await leads.GetByIdForSiteAsync(request.LeadId.Value, site.Id, ct);

        var captured = await leadCapture.CaptureAsync(
            site,
            new LeadCaptureInput(
                Channel: NormalizeChannel(request.Channel),
                ExternalContactId: request.ExternalContactId,
                ContactName: request.ContactName,
                FallbackMessage: request.Message,
                Fields: request.Fields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Conversation: BuildConversation(request),
                LeadId: existingLead?.Id,
                LeadCreatedAtUtc: existingLead?.CreatedAtUtc,
                NotifyOwner: request.NotifyOwner,
                IsTest: request.IsTest,
                ExplicitName: request.Name,
                ExplicitEmail: request.Email,
                ExplicitPhone: request.Phone),
            ct);

        return Ok(new
        {
            id = captured.Lead?.Id,
            siteId = captured.Lead?.SiteId,
            saved = captured.Saved
        });
    }

    private static string NormalizeChannel(string? channel)
    {
        return string.IsNullOrWhiteSpace(channel) ? "api" : channel.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<LeadCaptureTurn> BuildConversation(LeadIntakeRequest request)
    {
        if (request.Conversation is not null && request.Conversation.Count > 0)
        {
            return request.Conversation
                .Where(x => !string.IsNullOrWhiteSpace(x.Role) && !string.IsNullOrWhiteSpace(x.Text))
                .Select(x => new LeadCaptureTurn(x.Role!.Trim(), x.Text!.Trim(), x.AtUtc ?? DateTimeOffset.UtcNow))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.Message))
            return new[] { new LeadCaptureTurn("user", request.Message.Trim(), DateTimeOffset.UtcNow) };

        return Array.Empty<LeadCaptureTurn>();
    }

    public sealed class LeadIntakeRequest
    {
        public Guid? LeadId { get; init; }
        public string? SiteId { get; init; }
        public string? Channel { get; init; }
        public string? ExternalContactId { get; init; }
        public string? ContactName { get; init; }
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? Message { get; init; }
        public Dictionary<string, string>? Fields { get; init; }
        public List<ConversationTurnRequest>? Conversation { get; init; }
        public bool NotifyOwner { get; init; }
        public bool IsTest { get; init; }
    }

    public sealed class ConversationTurnRequest
    {
        public string? Role { get; init; }
        public string? Text { get; init; }
        public DateTimeOffset? AtUtc { get; init; }
    }
}
