using Mundialito.Api.Common;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Players.Commands.CreatePlayer;
using Mundialito.Application.Players.Commands.PatchPlayer;
using Mundialito.Application.Players.Commands.UpdatePlayer;
using Mundialito.Application.Players.Queries.GetPlayers;
using Mundialito.Domain.Players;

namespace Mundialito.Api.Contracts.Players;

/// <summary>Query string of GET /api/players and GET /api/teams/{teamId}/players.</summary>
public sealed record GetPlayersRequest(
    string? Search,
    string? Position,
    bool? IsActive,
    int? PageNumber,
    int? PageSize,
    string? SortBy,
    string? SortDirection)
{
    /// <summary>Sentinel outside the enum so the validator reports a clean message for bad input.</summary>
    private const PlayerPosition InvalidPosition = (PlayerPosition)(-1);

    public GetPlayersQuery ToQuery(Guid? teamId) => new()
    {
        TeamId = teamId,
        Search = Search,
        Position = ParsePosition(Position),
        IsActive = IsActive,
        PageNumber = PageNumber ?? PagingDefaults.DefaultPageNumber,
        PageSize = PageSize ?? PagingDefaults.DefaultPageSize,
        SortBy = SortBy,
        SortDirection = PagingRequest.ParseSortDirection(SortDirection),
    };

    private static PlayerPosition? ParsePosition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<PlayerPosition>(value, ignoreCase: true, out var position) && Enum.IsDefined(position)
            ? position
            : InvalidPosition;
    }
}

public sealed record CreatePlayerRequest(
    string? FirstName,
    string? LastName,
    int JerseyNumber,
    PlayerPosition Position,
    DateOnly? BirthDate)
{
    public CreatePlayerCommand ToCommand(Guid teamId) =>
        new(teamId, FirstName, LastName, JerseyNumber, Position, BirthDate);
}

public sealed record UpdatePlayerRequest(
    string? FirstName,
    string? LastName,
    int JerseyNumber,
    PlayerPosition Position,
    DateOnly? BirthDate,
    bool IsActive = true)
{
    public UpdatePlayerCommand ToCommand(Guid id, byte[]? expectedRowVersion) =>
        new(id, FirstName, LastName, JerseyNumber, Position, BirthDate, IsActive, expectedRowVersion);
}

public sealed record PatchPlayerRequest(
    string? FirstName,
    string? LastName,
    int? JerseyNumber,
    PlayerPosition? Position,
    DateOnly? BirthDate,
    bool? IsActive)
{
    public PatchPlayerCommand ToCommand(Guid id, byte[]? expectedRowVersion) =>
        new(id, FirstName, LastName, JerseyNumber, Position, BirthDate, IsActive, expectedRowVersion);
}
