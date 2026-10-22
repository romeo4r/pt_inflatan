using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Queries.GetTeamById;

public sealed record GetTeamByIdQuery(Guid Id) : IQuery<TeamResponse>;
