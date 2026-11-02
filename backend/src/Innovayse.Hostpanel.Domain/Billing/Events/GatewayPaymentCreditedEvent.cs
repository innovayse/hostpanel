using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// A hosted gateway payment took money its invoice could not record: the part above what was still
/// due, or the whole payment when another path had already settled or closed the invoice. The money
/// is on the ledger under the gateway's reference and either went to the client's credit balance or
/// is held for an operator. The event is published after the save and the ledger row makes a replay
/// find it, so a replay does not alert again.
/// </summary>
/// <param name="InvoiceId">The invoice the session was opened for.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="Module">The payment plugin id that took the money.</param>
/// <param name="Reference">The gateway's reference of the payment.</param>
/// <param name="Amount">The amount the invoice could not record, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code of the amount.</param>
/// <param name="Credited">Whether the amount went to the client's credit balance; <see langword="false"/> when it is held for an operator.</param>
public sealed record GatewayPaymentCreditedEvent(
    int InvoiceId, int ClientId, string Module, string Reference, decimal Amount, string Currency, bool Credited) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
