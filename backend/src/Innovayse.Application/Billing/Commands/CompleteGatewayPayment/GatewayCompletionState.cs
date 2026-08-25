namespace Innovayse.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
/// Outcome of a <see cref="CompleteGatewayPaymentCommand"/> attempt: what *our* completion
/// logic concluded after checking the invoice and, when needed, the gateway. Distinct from
/// <see cref="Innovayse.SDK.Plugins.GatewayPaymentState"/>, which describes what the bank
/// reported for a single status query — this enum also covers the idempotent
/// already-paid/already-refunded short-circuit that never calls the gateway at all.
/// </summary>
public enum GatewayCompletionState
{
    /// <summary>The invoice is paid — either just now, already paid, or already refunded.</summary>
    Paid,

    /// <summary>The gateway session is still open; the payer may still complete it.</summary>
    Pending,

    /// <summary>The gateway session was declined, cancelled, or is unknown to the gateway.</summary>
    Declined,
}
