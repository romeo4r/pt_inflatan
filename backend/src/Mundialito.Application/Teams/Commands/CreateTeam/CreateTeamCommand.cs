using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.CreateTeam;

/// <summary>Creates a team and returns its id. Raises TeamCreatedDomainEvent through the aggregate.</summary>
public sealed record CreateTeamCommand(string? Name, string? Code, string? Coach) : ICommand<Guid>;
