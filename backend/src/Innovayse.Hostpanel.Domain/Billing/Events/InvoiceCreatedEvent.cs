using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised when a new invoice is created.</summary>
/// <param name="InvoiceId">The invoice ID (0 before save; EF sets the real value post-persist).</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="Total">The invoice total when the event is dispatched, after the items were added.</param>
/// <param name="Currency">The ISO 4217 code the total is billed in.</param>
public record InvoiceCreatedEvent(int InvoiceId, int ClientId, decimal Total, string Currency) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
