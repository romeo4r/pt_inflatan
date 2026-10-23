using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.CancelMatch;

/// <summary>
/// Marks a scheduled match as cancelled. Idempotent at the domain level: cancelling twice is a
/// no-op. A finished match cannot be cancelled.
/// </summary>
public sealed record CancelMatchCommand(Guid Id, byte[]? ExpectedRowVersion = null) : ICommand;
