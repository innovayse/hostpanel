using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Domains.Events;

/// <summary>Raised when a domain registration is activated by the registrar.</summary>
/// <param name="DomainId">The domain ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="Name">The registered domain name.</param>
public record DomainRegisteredEvent(int DomainId, int ClientId, string Name) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
