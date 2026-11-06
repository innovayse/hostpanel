using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when a client chooses to pay an invoice by manual bank transfer and the invoice begins waiting for staff
/// to confirm it.
/// </summary>
/// <param name="InvoiceId">The invoice that waits for the transfer.</param>
/// <param name="ClientId">The client who chose it.</param>
/// <param name="Reference">The reference the payer is told to write on the transfer.</param>
public sealed record BankTransferRequestedEvent(int InvoiceId, int ClientId, string Reference) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
