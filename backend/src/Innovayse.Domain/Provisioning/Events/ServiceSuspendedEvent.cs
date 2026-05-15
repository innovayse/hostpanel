namespace Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Domain event raised when a hosting service has been suspended on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ClientId">Identifier of the client who owns the service.</param>
/// <param name="Reason">Human-readable reason for the suspension.</param>
public record ServiceSuspendedEvent(int ServiceId, int ClientId, string Reason) : IDomainEvent;
