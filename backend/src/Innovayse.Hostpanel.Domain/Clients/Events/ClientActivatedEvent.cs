using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>Domain event raised when a suspended or inactive <see cref="Client"/> becomes active again.</summary>
/// <param name="ClientId">The reactivated client's ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientActivatedEvent(int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
