using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.CreatePlayer;

/// <summary>Registers a player in a team's roster and returns the player id.</summary>
public sealed record CreatePlayerCommand(
    Guid TeamId,
    string? FirstName,
    string? LastName,
    int JerseyNumber,
    PlayerPosition Position,
    DateOnly? BirthDate) : ICommand<Guid>;
