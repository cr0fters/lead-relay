using LeadRelay.Domain.Leads;

namespace LeadRelay.Web.Leads;

public sealed record LeadCaptureResult(
    Lead? Lead,
    bool LeadJustCreated,
    bool Saved);
