using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.DeleteMatch;

/// <summary>
/// Removes a fixture that was never played. Idempotent: a match that no longer exists is a
/// success. A finished match is history (standings, scorers) and cannot be deleted.
/// </summary>
public sealed record DeleteMatchCommand(Guid Id) : ICommand;
