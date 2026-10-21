using Mundialito.Domain.Common;

namespace Mundialito.Domain.Teams.Events;

/// <summary>Raised once when a team is created. Required by the test specification.</summary>
public sealed record TeamCreatedDomainEvent(Guid TeamId, string Name, string Code) : DomainEvent;
