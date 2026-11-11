using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Provisioning.Events;

/// <summary>
/// Domain event raised when a hosting service has been successfully provisioned on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ClientId">Identifier of the client who owns the service.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the newly created account.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ServiceProvisionedEvent(int ServiceId, int ClientId, string ProvisioningRef, DateTimeOffset OccurredAt) : IDomainEvent;
