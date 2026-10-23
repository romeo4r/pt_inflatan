namespace Mundialito.Api.Common;

/// <summary>
/// The API stores and compares every instant in UTC. JSON dates arrive with an offset ("Z" or
/// "+06:00"), which System.Text.Json converts to Local or Utc kind, or without one (Unspecified),
/// which is taken as UTC. This makes the stored value independent of the server's time zone.
/// </summary>
public static class UtcDateTime
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? Normalize(DateTime? value) => value is null ? null : Normalize(value.Value);
}
