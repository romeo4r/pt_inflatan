using Mundialito.Application.Abstractions.Time;

namespace Mundialito.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
