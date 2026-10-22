using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Matches;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class MatchRepository(ApplicationDbContext context) : IMatchRepository
{
    // Goals are auto-included by configuration, so the aggregate is always loaded whole.
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Matches.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> AnyInvolvingTeamAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        context.Matches.AnyAsync(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId, cancellationToken);

    public void Add(Match match) => context.Matches.Add(match);

    public void Remove(Match match) => context.Matches.Remove(match);
}
