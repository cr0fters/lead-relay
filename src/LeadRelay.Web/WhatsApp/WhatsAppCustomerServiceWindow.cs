using LeadRelay.Domain.Leads;

namespace LeadRelay.Web.WhatsApp;

public static class WhatsAppCustomerServiceWindow
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(24);

    public static WhatsAppCustomerServiceWindowStatus Evaluate(
        IEnumerable<LeadConversationTurn> conversation,
        DateTimeOffset now)
    {
        DateTimeOffset? lastInboundAtUtc = null;
        foreach (var turn in conversation)
        {
            if (IsCustomerRole(turn.Role) &&
                (lastInboundAtUtc is null || turn.AtUtc > lastInboundAtUtc.Value))
            {
                lastInboundAtUtc = turn.AtUtc;
            }
        }

        if (lastInboundAtUtc is null)
            return new WhatsAppCustomerServiceWindowStatus(false, null, null);

        var closesAtUtc = lastInboundAtUtc.Value > DateTimeOffset.MaxValue - Duration
            ? DateTimeOffset.MaxValue
            : lastInboundAtUtc.Value + Duration;
        var isOpen = lastInboundAtUtc.Value <= now && now < closesAtUtc;
        return new WhatsAppCustomerServiceWindowStatus(isOpen, lastInboundAtUtc, closesAtUtc);
    }

    private static bool IsCustomerRole(string? role)
        => string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "customer", StringComparison.OrdinalIgnoreCase);
}

public sealed record WhatsAppCustomerServiceWindowStatus(
    bool IsOpen,
    DateTimeOffset? LastInboundAtUtc,
    DateTimeOffset? ClosesAtUtc);
