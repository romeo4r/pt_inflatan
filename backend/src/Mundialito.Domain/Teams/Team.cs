using Mundialito.Domain.Common;
using Mundialito.Domain.Teams.Events;

namespace Mundialito.Domain.Teams;

/// <summary>
/// A team taking part in the tournament. Aggregate root: players and matches reference it by id.
/// </summary>
public sealed class Team : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int CodeMinLength = 2;
    public const int CodeMaxLength = 10;
    public const int CoachMaxLength = 100;

    private Team(Guid id, string name, string code, string? coach)
        : base(id)
    {
        Name = name;
        Code = code;
        Coach = coach;
        IsActive = true;
    }

    /// <summary>Required by EF Core.</summary>
    private Team()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>Short unique identifier shown in standings and fixtures, e.g. "FIN".</summary>
    public string Code { get; private set; } = null!;

    public string? Coach { get; private set; }

    /// <summary>Soft-delete flag: inactive teams are hidden by default but keep their history.</summary>
    public bool IsActive { get; private set; }

    public static Result<Team> Create(string? name, string? code, string? coach)
    {
        var validation = Validate(name, code, coach);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var team = new Team(NewId(), name!.Trim(), NormalizeCode(code!), NormalizeOptional(coach));
        team.RaiseDomainEvent(new TeamCreatedDomainEvent(team.Id, team.Name, team.Code));

        return team;
    }

    /// <summary>Full replacement of editable data (PUT semantics).</summary>
    public Result Update(string? name, string? code, string? coach)
    {
        var validation = Validate(name, code, coach);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name!.Trim();
        Code = NormalizeCode(code!);
        Coach = NormalizeOptional(coach);

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static Result Validate(string? name, string? code, string? coach)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(TeamErrors.NameRequired);
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return Result.Failure(TeamErrors.NameTooLong);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(TeamErrors.CodeRequired);
        }

        var normalizedCode = NormalizeCode(code);
        if (normalizedCode.Length is < CodeMinLength or > CodeMaxLength
            || !normalizedCode.All(char.IsLetterOrDigit))
        {
            return Result.Failure(TeamErrors.CodeInvalid);
        }

        if (coach is not null && coach.Trim().Length > CoachMaxLength)
        {
            return Result.Failure(TeamErrors.CoachTooLong);
        }

        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
