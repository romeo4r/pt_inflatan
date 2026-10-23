using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

/// <summary>Full replacement (PUT): every editable field is required and overwritten. Idempotent.</summary>
public sealed record UpdateTeamCommand(Guid Id, string? Name, string? Code, string? Coach, bool IsActive) : ICommand;
