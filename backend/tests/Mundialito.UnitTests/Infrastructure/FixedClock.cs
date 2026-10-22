using Mundialito.Application.Abstractions.Time;

namespace Mundialito.UnitTests.Infrastructure;

internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}
