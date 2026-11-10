using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Domains.Events;

/// <summary>Raised by a scheduled job when a domain is approaching its expiry date.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="Name">The domain name.</param>
/// <param name="ExpiresAt">The date the domain will expire.</param>
public record DomainExpiringEvent(int DomainId, int ClientId, string Name, DateTimeOffset ExpiresAt) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
