using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.UpdatePlayer;

/// <summary>
/// Full replacement (PUT) of a player's editable data. Team membership is immutable: moving a
/// player would break goal attribution, so it is modelled as delete + create in the other team.
/// </summary>
public sealed record UpdatePlayerCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    int JerseyNumber,
    PlayerPosition Position,
    DateOnly? BirthDate,
    bool IsActive,
    byte[]? ExpectedRowVersion = null) : ICommand;
