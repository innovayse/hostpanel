namespace Innovayse.Application.Billing.Commands.AddInvoicePayment;

/// <summary>Command to record a manual payment against an invoice.</summary>
/// <param name="InvoiceId">The invoice to record payment on.</param>
/// <param name="Date">UTC timestamp of the payment.</param>
/// <param name="Gateway">Payment gateway name (e.g. "Stripe", "Manual").</param>
/// <param name="TransactionId">External transaction reference.</param>
/// <param name="Amount">Payment amount.</param>
/// <param name="Fees">Transaction fees charged by the gateway.</param>
/// <param name="Notes">Optional payment notes.</param>
public record AddInvoicePaymentCommand(
    int InvoiceId,
    DateTimeOffset Date,
    string Gateway,
    string TransactionId,
    decimal Amount,
    decimal Fees = 0m,
    string? Notes = null);
