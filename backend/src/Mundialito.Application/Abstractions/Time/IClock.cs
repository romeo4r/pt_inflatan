namespace Mundialito.Application.Abstractions.Time;

/// <summary>
/// Source of the current time. Injected instead of calling <see cref="DateTime.UtcNow"/> directly
/// so handlers are deterministic under test.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
