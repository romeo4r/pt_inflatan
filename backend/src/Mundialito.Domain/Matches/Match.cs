using Mundialito.Domain.Common;
using Mundialito.Domain.Matches.Events;

namespace Mundialito.Domain.Matches;

/// <summary>
/// A fixture between two teams. Aggregate root that owns its goals and enforces the lifecycle
/// Scheduled -> Finished | Cancelled.
/// </summary>
public sealed class Match : AuditableEntity
{
    public const int VenueMaxLength = 150;

    private readonly List<Goal> _goals = [];

    private Match(Guid id, Guid homeTeamId, Guid awayTeamId, int matchDay, DateTime scheduledAtUtc, string? venue)
        : base(id)
    {
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        MatchDay = matchDay;
        ScheduledAtUtc = scheduledAtUtc;
        Venue = venue;
        Status = MatchStatus.Scheduled;
    }

    /// <summary>Required by EF Core.</summary>
    private Match()
    {
    }

    public Guid HomeTeamId { get; private set; }

    public Guid AwayTeamId { get; private set; }

    public int MatchDay { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public string? Venue { get; private set; }

    public MatchStatus Status { get; private set; }

    /// <summary>Null until a result is registered.</summary>
    public int? HomeScore { get; private set; }

    /// <summary>Null until a result is registered.</summary>
    public int? AwayScore { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public IReadOnlyCollection<Goal> Goals => _goals.AsReadOnly();

    public static Result<Match> Schedule(
        Guid homeTeamId,
        Guid awayTeamId,
        int matchDay,
        DateTime scheduledAtUtc,
        string? venue)
    {
        if (homeTeamId == awayTeamId)
        {
            return MatchErrors.SameTeam;
        }

        var validation = ValidateSchedule(matchDay, venue);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Match(NewId(), homeTeamId, awayTeamId, matchDay, scheduledAtUtc, NormalizeOptional(venue));
    }

    /// <summary>Changes fixture data. Only allowed while the match has not been played or cancelled.</summary>
    public Result Reschedule(int matchDay, DateTime scheduledAtUtc, string? venue)
    {
        if (Status != MatchStatus.Scheduled)
        {
            return Result.Failure(MatchErrors.NotScheduled);
        }

        var validation = ValidateSchedule(matchDay, venue);
        if (validation.IsFailure)
        {
            return validation;
        }

        MatchDay = matchDay;
        ScheduledAtUtc = scheduledAtUtc;
        Venue = NormalizeOptional(venue);

        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == MatchStatus.Finished)
        {
            return Result.Failure(MatchErrors.AlreadyFinished);
        }

        // Cancelling an already cancelled match is a no-op, which keeps the operation idempotent.
        Status = MatchStatus.Cancelled;

        return Result.Success();
    }

    /// <summary>
    /// Records the final score and the goals that produced it, then raises
    /// <see cref="MatchResultRegisteredDomainEvent"/>. Invariants: the match must be scheduled,
    /// scores cannot be negative, every goal must belong to one of the two teams, and goals per
    /// team must add up to the score. Player membership in the team is verified by the application
    /// layer, which is the one able to query players.
    /// </summary>
    public Result RegisterResult(
        int homeScore,
        int awayScore,
        IReadOnlyCollection<GoalEntry> goals,
        DateTime finishedAtUtc)
    {
        if (Status == MatchStatus.Finished)
        {
            return Result.Failure(MatchErrors.AlreadyFinished);
        }

        if (Status == MatchStatus.Cancelled)
        {
            return Result.Failure(MatchErrors.Cancelled);
        }

        if (homeScore < 0 || awayScore < 0)
        {
            return Result.Failure(MatchErrors.NegativeScore);
        }

        if (goals.Any(g => g.Minute is < Goal.MinMinute or > Goal.MaxMinute))
        {
            return Result.Failure(MatchErrors.MinuteOutOfRange);
        }

        if (goals.Any(g => !Involves(g.TeamId)))
        {
            return Result.Failure(MatchErrors.ScorerTeamNotInMatch);
        }

        var homeGoals = goals.Count(g => g.TeamId == HomeTeamId);
        var awayGoals = goals.Count(g => g.TeamId == AwayTeamId);
        if (homeGoals != homeScore || awayGoals != awayScore)
        {
            return Result.Failure(MatchErrors.GoalsDoNotMatchScore);
        }

        HomeScore = homeScore;
        AwayScore = awayScore;
        FinishedAtUtc = finishedAtUtc;
        Status = MatchStatus.Finished;

        foreach (var entry in goals)
        {
            _goals.Add(new Goal(NewId(), Id, entry.PlayerId, entry.TeamId, entry.Minute));
        }

        RaiseDomainEvent(new MatchResultRegisteredDomainEvent(Id, HomeTeamId, AwayTeamId, homeScore, awayScore));

        return Result.Success();
    }

    public bool Involves(Guid teamId) => HomeTeamId == teamId || AwayTeamId == teamId;

    private static Result ValidateSchedule(int matchDay, string? venue)
    {
        if (matchDay < 1)
        {
            return Result.Failure(MatchErrors.MatchDayInvalid);
        }

        if (venue is not null && venue.Trim().Length > VenueMaxLength)
        {
            return Result.Failure(MatchErrors.VenueTooLong);
        }

        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
