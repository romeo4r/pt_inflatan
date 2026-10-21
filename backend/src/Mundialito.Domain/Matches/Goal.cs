using Mundialito.Domain.Common;

namespace Mundialito.Domain.Matches;

/// <summary>
/// One goal scored in a match. Child of the <see cref="Match"/> aggregate: it is only created
/// through <see cref="Match.RegisterResult"/>, so the score and the goal list can never disagree.
/// </summary>
public sealed class Goal : Entity
{
    public const int MinMinute = 1;
    public const int MaxMinute = 120;

    internal Goal(Guid id, Guid matchId, Guid playerId, Guid teamId, int? minute)
        : base(id)
    {
        MatchId = matchId;
        PlayerId = playerId;
        TeamId = teamId;
        Minute = minute;
    }

    /// <summary>Required by EF Core.</summary>
    private Goal()
    {
    }

    public Guid MatchId { get; private set; }

    public Guid PlayerId { get; private set; }

    /// <summary>Team credited with the goal. Stored explicitly so scorer queries avoid a join.</summary>
    public Guid TeamId { get; private set; }

    public int? Minute { get; private set; }
}

/// <summary>Input describing a goal when a result is registered.</summary>
public sealed record GoalEntry(Guid PlayerId, Guid TeamId, int? Minute);
