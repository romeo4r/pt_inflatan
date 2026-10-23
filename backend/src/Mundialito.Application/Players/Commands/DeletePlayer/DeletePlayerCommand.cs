using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Players.Commands.DeletePlayer;

/// <summary>
/// Removes a player. Idempotent: a player that no longer exists is a success. Players credited
/// with goals cannot be deleted because the top-scorers history must stay consistent.
/// </summary>
public sealed record DeletePlayerCommand(Guid Id) : ICommand;
