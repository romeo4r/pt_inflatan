using Mundialito.Domain.Teams;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>Write-side access to the Team aggregate. Reads for listing live on the Dapper side.</summary>
public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    void Add(Team team);

    void Remove(Team team);
}
