using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// Raised when staff confirm that a manual bank transfer arrived. It comes with the usual
/// <see cref="PaymentReceivedEvent"/>; this one tells the client that the transfer they made was matched.
/// </summary>
/// <param name="InvoiceId">The invoice that was paid.</param>
/// <param name="ClientId">The client who paid.</param>
/// <param name="Reference">The reference the transfer was matched by.</param>
public sealed record BankTransferConfirmedEvent(int InvoiceId, int ClientId, string Reference) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
