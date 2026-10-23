using Mundialito.Api.Common;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;
using Mundialito.Application.Teams.Queries.GetTeams;

namespace Mundialito.Api.Contracts.Teams;

/// <summary>Query string of GET /api/teams. Mapped to the application query with safe defaults.</summary>
public sealed record GetTeamsRequest(
    string? Search,
    bool? IsActive,
    int? PageNumber,
    int? PageSize,
    string? SortBy,
    string? SortDirection)
{
    public GetTeamsQuery ToQuery() => new()
    {
        Search = Search,
        IsActive = IsActive,
        PageNumber = PageNumber ?? PagingDefaults.DefaultPageNumber,
        PageSize = PageSize ?? PagingDefaults.DefaultPageSize,
        SortBy = SortBy,
        SortDirection = PagingRequest.ParseSortDirection(SortDirection),
    };
}

public sealed record CreateTeamRequest(string? Name, string? Code, string? Coach)
{
    public CreateTeamCommand ToCommand() => new(Name, Code, Coach);
}

public sealed record UpdateTeamRequest(string? Name, string? Code, string? Coach, bool IsActive = true)
{
    public UpdateTeamCommand ToCommand(Guid id) => new(id, Name, Code, Coach, IsActive);
}

public sealed record PatchTeamRequest(string? Name, string? Code, string? Coach, bool? IsActive)
{
    public PatchTeamCommand ToCommand(Guid id) => new(id, Name, Code, Coach, IsActive);
}
