namespace Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Domain event raised when a hosting service has been unsuspended and restored to active status.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ClientId">Identifier of the client who owns the service.</param>
public record ServiceUnsuspendedEvent(int ServiceId, int ClientId) : IDomainEvent;
