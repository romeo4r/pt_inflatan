using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.PatchTeam;

/// <summary>
/// Partial update (PATCH): only non-null members are applied, the rest keep their current value.
/// To clear the coach send an empty string, which the domain normalizes to null.
/// <paramref name="ExpectedRowVersion"/> comes from the If-Match header (optional).
/// </summary>
public sealed record PatchTeamCommand(
    Guid Id,
    string? Name,
    string? Code,
    string? Coach,
    bool? IsActive,
    byte[]? ExpectedRowVersion = null) : ICommand
{
    public bool HasChanges => Name is not null || Code is not null || Coach is not null || IsActive is not null;
}
