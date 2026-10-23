using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

/// <summary>
/// Full replacement (PUT): every editable field is required and overwritten. Idempotent.
/// <paramref name="ExpectedRowVersion"/> comes from the If-Match header; when present the update
/// only succeeds if nobody changed the team since the client read it.
/// </summary>
public sealed record UpdateTeamCommand(
    Guid Id,
    string? Name,
    string? Code,
    string? Coach,
    bool IsActive,
    byte[]? ExpectedRowVersion = null) : ICommand;
