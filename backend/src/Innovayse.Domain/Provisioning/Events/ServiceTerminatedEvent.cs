namespace Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Domain event raised when a hosting service has been permanently terminated on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ClientId">Identifier of the client who owns the service.</param>
/// <param name="Reason">Human-readable reason for the termination.</param>
public record ServiceTerminatedEvent(int ServiceId, int ClientId, string Reason) : IDomainEvent;
