using Mundialito.Domain.Common;

namespace Mundialito.Domain.Players;

/// <summary>
/// A member of a team's roster. Belongs to exactly one team for the whole tournament,
/// which keeps goal attribution and the top-scorers table unambiguous.
/// </summary>
public sealed class Player : AuditableEntity
{
    public const int NameMaxLength = 80;
    public const int MinJerseyNumber = 1;
    public const int MaxJerseyNumber = 99;

    private Player(
        Guid id,
        Guid teamId,
        string firstName,
        string lastName,
        int jerseyNumber,
        PlayerPosition position,
        DateOnly? birthDate)
        : base(id)
    {
        TeamId = teamId;
        FirstName = firstName;
        LastName = lastName;
        JerseyNumber = jerseyNumber;
        Position = position;
        BirthDate = birthDate;
        IsActive = true;
    }

    /// <summary>Required by EF Core.</summary>
    private Player()
    {
    }

    public Guid TeamId { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string FullName => $"{FirstName} {LastName}";

    public int JerseyNumber { get; private set; }

    public PlayerPosition Position { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Player> Create(
        Guid teamId,
        string? firstName,
        string? lastName,
        int jerseyNumber,
        PlayerPosition position,
        DateOnly? birthDate,
        DateOnly today)
    {
        var validation = Validate(firstName, lastName, jerseyNumber, position, birthDate, today);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Player(
            NewId(), teamId, firstName!.Trim(), lastName!.Trim(), jerseyNumber, position, birthDate);
    }

    /// <summary>Full replacement of editable data (PUT semantics). Team membership is immutable.</summary>
    public Result Update(
        string? firstName,
        string? lastName,
        int jerseyNumber,
        PlayerPosition position,
        DateOnly? birthDate,
        DateOnly today)
    {
        var validation = Validate(firstName, lastName, jerseyNumber, position, birthDate, today);
        if (validation.IsFailure)
        {
            return validation;
        }

        FirstName = firstName!.Trim();
        LastName = lastName!.Trim();
        JerseyNumber = jerseyNumber;
        Position = position;
        BirthDate = birthDate;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static Result Validate(
        string? firstName,
        string? lastName,
        int jerseyNumber,
        PlayerPosition position,
        DateOnly? birthDate,
        DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Result.Failure(PlayerErrors.FirstNameRequired);
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure(PlayerErrors.LastNameRequired);
        }

        if (firstName.Trim().Length > NameMaxLength || lastName.Trim().Length > NameMaxLength)
        {
            return Result.Failure(PlayerErrors.NameTooLong);
        }

        if (jerseyNumber is < MinJerseyNumber or > MaxJerseyNumber)
        {
            return Result.Failure(PlayerErrors.JerseyNumberOutOfRange);
        }

        if (!Enum.IsDefined(position))
        {
            return Result.Failure(PlayerErrors.PositionInvalid);
        }

        if (birthDate is not null && birthDate > today)
        {
            return Result.Failure(PlayerErrors.BirthDateInFuture);
        }

        return Result.Success();
    }
}
