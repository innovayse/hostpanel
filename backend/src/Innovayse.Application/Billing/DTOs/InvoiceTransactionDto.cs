namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO representing a financial transaction on an invoice.</summary>
/// <param name="Id">Transaction primary key.</param>
/// <param name="Date">UTC timestamp of the transaction.</param>
/// <param name="Gateway">Payment gateway name.</param>
/// <param name="TransactionId">External transaction reference.</param>
/// <param name="Amount">Transaction amount.</param>
/// <param name="Fees">Transaction fees charged by the gateway.</param>
/// <param name="Type">Transaction type (Payment, Refund, Credit).</param>
/// <param name="Notes">Optional notes; null when not provided.</param>
public record InvoiceTransactionDto(
    int Id,
    DateTimeOffset Date,
    string Gateway,
    string TransactionId,
    decimal Amount,
    decimal Fees,
    InvoiceTransactionType Type,
    string? Notes);
