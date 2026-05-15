namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a domain transitions to the Expired status.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="LinkedServiceId">The linked hosting service ID, if any; null otherwise.</param>
public record DomainExpiredEvent(int DomainId, int ClientId, int? LinkedServiceId) : IDomainEvent;
