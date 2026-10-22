using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class TeamRepository(ApplicationDbContext context) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Teams.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        context.Teams.AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = Team.NormalizeCode(code);
        return context.Teams.AnyAsync(t => t.Code == normalized && (excludeId == null || t.Id != excludeId), cancellationToken);
    }

    public void Add(Team team) => context.Teams.Add(team);

    public void Remove(Team team) => context.Teams.Remove(team);
}
