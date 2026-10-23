using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.PatchPlayer;

/// <summary>
/// Partial update (PATCH): only non-null members are applied. BirthDate cannot be cleared through
/// PATCH (null means "unchanged"); use PUT with a null birth date for that.
/// </summary>
public sealed record PatchPlayerCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    int? JerseyNumber,
    PlayerPosition? Position,
    DateOnly? BirthDate,
    bool? IsActive,
    byte[]? ExpectedRowVersion = null) : ICommand
{
    public bool HasChanges =>
        FirstName is not null || LastName is not null || JerseyNumber is not null
        || Position is not null || BirthDate is not null || IsActive is not null;
}
