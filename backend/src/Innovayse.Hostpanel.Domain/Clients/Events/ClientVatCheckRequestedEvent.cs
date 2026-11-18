using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>
/// A client's VAT number, or the country it is registered in, was saved and no completed check judges it yet, so the number
/// is to be checked against the VAT number service. Published after the save that changed it, and handled in the background,
/// so the person saving never waits for the service.
/// </summary>
/// <param name="ClientId">The client whose number is to be checked.</param>
public record ClientVatCheckRequestedEvent(int ClientId) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
