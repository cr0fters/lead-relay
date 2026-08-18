namespace LeadRelay.Domain.Projects;

public sealed record ProjectStageChange(
    string FromStage,
    string ToStage,
    DateTimeOffset AtUtc);
