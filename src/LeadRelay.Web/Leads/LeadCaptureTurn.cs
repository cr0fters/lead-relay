namespace LeadRelay.Web.Leads;

public sealed record LeadCaptureTurn(string Role, string Text, DateTimeOffset AtUtc);
