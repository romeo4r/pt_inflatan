using Mundialito.Domain.Matches;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>Write-side access to the Match aggregate, including its goals.</summary>
public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the team appears as home or away in any match. Used to block team deletion.</summary>
    Task<bool> AnyInvolvingTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    void Add(Match match);

    void Remove(Match match);
}
