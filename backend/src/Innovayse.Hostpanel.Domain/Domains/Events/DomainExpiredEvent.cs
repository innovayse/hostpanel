using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Domains.Events;

/// <summary>Raised when a domain transitions to the Expired status.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="LinkedServiceId">The linked hosting service ID, if any; null otherwise.</param>
public record DomainExpiredEvent(int DomainId, int ClientId, int? LinkedServiceId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
