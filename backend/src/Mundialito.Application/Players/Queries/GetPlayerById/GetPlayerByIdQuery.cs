using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Players.Queries.GetPlayerById;

public sealed record GetPlayerByIdQuery(Guid Id) : IQuery<PlayerResponse>;
