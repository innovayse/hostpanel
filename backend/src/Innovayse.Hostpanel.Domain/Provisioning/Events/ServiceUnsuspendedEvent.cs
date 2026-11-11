using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Provisioning.Events;

/// <summary>
/// Domain event raised when a hosting service has been unsuspended and restored to active status.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ClientId">Identifier of the client who owns the service.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ServiceUnsuspendedEvent(int ServiceId, int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
