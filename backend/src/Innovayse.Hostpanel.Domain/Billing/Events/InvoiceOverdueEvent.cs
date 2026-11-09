using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised when an invoice is marked overdue.</summary>
/// <param name="InvoiceId">The overdue invoice ID.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
public record InvoiceOverdueEvent(int InvoiceId, int ClientId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
