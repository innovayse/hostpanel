namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the card gateway needs to open a payment intent for an order's invoice.</summary>
/// <remarks>
/// The ids are carried so the gateway's own dashboard and reconciliation name the same rows this database
/// does: a payment that arrives with no way back to an order is a manual reconciliation.
/// </remarks>
/// <param name="Amount">The amount in the major currency unit (e.g. dollars).</param>
/// <param name="Currency">ISO 4217 currency code (e.g. "usd").</param>
/// <param name="OrderId">The order being paid.</param>
/// <param name="InvoiceId">The invoice the order is billed on.</param>
/// <param name="ClientId">The client paying.</param>
/// <param name="IdempotencyKey">
/// The key the gateway deduplicates the intent by: a repeated request for the same invoice attempt
/// answers with the first intent instead of opening a second one that could be paid as well.
/// </param>
public sealed record CardPaymentIntentInstruction(
    decimal Amount,
    string Currency,
    int OrderId,
    int InvoiceId,
    int ClientId,
    string IdempotencyKey);
