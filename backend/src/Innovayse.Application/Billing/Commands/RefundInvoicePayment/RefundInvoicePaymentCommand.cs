namespace Innovayse.Application.Billing.Commands.RefundInvoicePayment;

/// <summary>Command to refund a paid invoice.</summary>
/// <param name="InvoiceId">The paid invoice to refund.</param>
/// <param name="Amount">Refund amount (0 = full refund of the selected transaction).</param>
/// <param name="RefundType">How to process: Gateway, Manual, or CreditBalance.</param>
/// <param name="Gateway">Payment gateway name.</param>
/// <param name="TransactionId">Original payment transaction ID being refunded.</param>
/// <param name="RefundTransactionId">External refund reference (required for Manual refund type).</param>
/// <param name="Notes">Optional refund notes.</param>
public record RefundInvoicePaymentCommand(
    int InvoiceId,
    decimal Amount,
    string RefundType,
    string Gateway,
    string TransactionId,
    string? RefundTransactionId = null,
    string? Notes = null);
