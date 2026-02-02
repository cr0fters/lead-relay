using LeadRelay.Application.Abstractions;

namespace LeadRelay.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
