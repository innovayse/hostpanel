using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised when an invoice is cancelled, so whatever waited for that invoice can let go of it.</summary>
/// <param name="InvoiceId">The cancelled invoice ID.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
public record InvoiceCancelledEvent(int InvoiceId, int ClientId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
