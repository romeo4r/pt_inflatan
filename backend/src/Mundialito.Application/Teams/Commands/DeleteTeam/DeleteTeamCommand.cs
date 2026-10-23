using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.DeleteTeam;

/// <summary>
/// Removes a team and its roster. Idempotent: deleting a team that no longer exists succeeds,
/// because the end state ("the team is gone") is the same. Teams with fixtures cannot be deleted.
/// </summary>
public sealed record DeleteTeamCommand(Guid Id) : ICommand;
