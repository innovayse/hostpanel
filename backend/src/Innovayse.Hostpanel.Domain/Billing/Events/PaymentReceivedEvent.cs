using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>Raised when a payment is successfully recorded on an invoice.</summary>
/// <param name="InvoiceId">The paid invoice ID.</param>
/// <param name="ClientId">The client who paid.</param>
/// <param name="Amount">The amount paid.</param>
/// <param name="TransactionId">The gateway transaction reference.</param>
public record PaymentReceivedEvent(int InvoiceId, int ClientId, decimal Amount, string TransactionId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
