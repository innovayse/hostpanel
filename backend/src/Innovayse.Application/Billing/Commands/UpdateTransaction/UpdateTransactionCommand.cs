namespace Innovayse.Application.Billing.Commands.UpdateTransaction;

/// <summary>Command to update an existing transaction's editable fields.</summary>
/// <param name="Id">Transaction primary key.</param>
/// <param name="Date">UTC timestamp of the transaction.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="TransactionId">External transaction reference.</param>
/// <param name="InvoiceId">Optional related invoice ID.</param>
/// <param name="PaymentMethod">Payment method used.</param>
/// <param name="AmountIn">Amount credited (≥ 0).</param>
/// <param name="AmountOut">Amount debited (≥ 0).</param>
/// <param name="Fees">Transaction fees (≥ 0).</param>
public record UpdateTransactionCommand(
    int Id,
    DateTimeOffset Date,
    string Description,
    string TransactionId,
    int? InvoiceId,
    string PaymentMethod,
    decimal AmountIn,
    decimal AmountOut,
    decimal Fees);
