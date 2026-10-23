using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.PatchMatch;

/// <summary>
/// Partial update (PATCH) of fixture data. Null means "unchanged"; an empty venue clears it.
/// </summary>
public sealed record PatchMatchCommand(
    Guid Id,
    int? MatchDay,
    DateTime? ScheduledAtUtc,
    string? Venue,
    byte[]? ExpectedRowVersion = null) : ICommand
{
    public bool HasChanges => MatchDay is not null || ScheduledAtUtc is not null || Venue is not null;
}
