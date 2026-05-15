namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>Raised by a scheduled job when a domain is approaching its expiry date.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="Name">The domain name.</param>
/// <param name="ExpiresAt">The date the domain will expire.</param>
public record DomainExpiringEvent(int DomainId, int ClientId, string Name, DateTimeOffset ExpiresAt) : IDomainEvent;
