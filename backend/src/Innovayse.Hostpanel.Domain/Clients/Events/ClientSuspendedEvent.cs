using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>Domain event raised when a <see cref="Client"/> is suspended; the account stays on file but may no longer order or pay.</summary>
/// <param name="ClientId">The suspended client's ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientSuspendedEvent(int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
