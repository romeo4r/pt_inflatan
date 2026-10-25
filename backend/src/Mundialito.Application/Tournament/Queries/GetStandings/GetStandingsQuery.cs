using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Tournament.Queries.GetStandings;

/// <summary>The current table. Not paged: a tournament has a handful of teams.</summary>
public sealed record GetStandingsQuery : IQuery<StandingsResponse>;
