using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// A hosted gateway reported the payment an invoice was settled through as no longer paid: the status read after the
/// settlement came back declined, which the plugin contract also uses for a refund, a reversal and a charge-back at the
/// gateway. The host cannot tell a partial return from a full one, nor a refund it made itself from one made at the
/// gateway, so it changes nothing on the invoice or the ledger and tells staff. The event is published once per
/// session, after the save that marks it, and only when the invoice shows no refund through that gateway.
/// </summary>
/// <param name="InvoiceId">The invoice the session was opened for.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="Module">The payment plugin id that took the money.</param>
public sealed record GatewayPaymentReversedEvent(int InvoiceId, int ClientId, string Module) : IDomainEvent
{
    /// <summary>When the reversal was noticed, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
