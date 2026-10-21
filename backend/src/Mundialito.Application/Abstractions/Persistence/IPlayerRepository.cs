using Mundialito.Domain.Players;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>Write-side access to the Player aggregate.</summary>
public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads several players at once, used to validate scorers when a result is registered.</summary>
    Task<IReadOnlyList<Player>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> ExistsByJerseyNumberAsync(
        Guid teamId,
        int jerseyNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    void Add(Player player);

    void Remove(Player player);
}
