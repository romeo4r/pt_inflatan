using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Players;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class PlayerRepository(ApplicationDbContext context) : IPlayerRepository
{
    public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await context.Players
            .Where(p => distinctIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByJerseyNumberAsync(
        Guid teamId,
        int jerseyNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        context.Players.AnyAsync(
            p => p.TeamId == teamId && p.JerseyNumber == jerseyNumber && (excludeId == null || p.Id != excludeId),
            cancellationToken);

    public void Add(Player player) => context.Players.Add(player);

    public void Remove(Player player) => context.Players.Remove(player);
}
