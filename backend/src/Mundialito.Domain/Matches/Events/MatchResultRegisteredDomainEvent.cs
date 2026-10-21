using Mundialito.Domain.Common;

namespace Mundialito.Domain.Matches.Events;

/// <summary>Raised once when a match result is registered. Required by the test specification.</summary>
public sealed record MatchResultRegisteredDomainEvent(
    Guid MatchId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore) : DomainEvent;
