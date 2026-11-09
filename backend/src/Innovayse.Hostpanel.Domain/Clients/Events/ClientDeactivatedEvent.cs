using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>Domain event raised when an active <see cref="Client"/> is marked inactive.</summary>
/// <param name="ClientId">The deactivated client's ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientDeactivatedEvent(int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
