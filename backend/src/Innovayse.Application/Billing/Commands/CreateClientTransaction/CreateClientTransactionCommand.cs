namespace Innovayse.Application.Billing.Commands.CreateClientTransaction;

/// <summary>Command to create a new client transaction and optionally adjust credit balance.</summary>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Date">UTC timestamp of the transaction.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="TransactionId">External transaction reference.</param>
/// <param name="InvoiceId">Optional related invoice ID.</param>
/// <param name="PaymentMethod">Payment method used.</param>
/// <param name="AmountIn">Amount credited to the account (≥ 0).</param>
/// <param name="AmountOut">Amount debited from the account (≥ 0).</param>
/// <param name="Fees">Transaction fees (≥ 0).</param>
/// <param name="AddToCredit">When true, adjusts the client's credit balance accordingly.</param>
public record CreateClientTransactionCommand(
    int ClientId,
    DateTimeOffset Date,
    string Description,
    string TransactionId,
    int? InvoiceId,
    string PaymentMethod,
    decimal AmountIn,
    decimal AmountOut,
    decimal Fees,
    bool AddToCredit);
