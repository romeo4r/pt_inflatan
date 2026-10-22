using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Queries.GetMatchById;

public sealed record GetMatchByIdQuery(Guid Id) : IQuery<MatchDetailResponse>;
