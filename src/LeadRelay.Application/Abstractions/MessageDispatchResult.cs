namespace LeadRelay.Application.Abstractions;

public sealed record MessageDispatchResult(bool Sent, string? Error = null);
