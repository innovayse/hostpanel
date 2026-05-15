namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a domain is successfully renewed.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="NewExpiresAt">The new expiry date after renewal.</param>
public record DomainRenewedEvent(int DomainId, int ClientId, DateTimeOffset NewExpiresAt) : IDomainEvent;
