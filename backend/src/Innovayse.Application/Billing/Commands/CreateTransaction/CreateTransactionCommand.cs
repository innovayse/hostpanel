namespace Innovayse.Application.Billing.Commands.CreateTransaction;

/// <summary>Command to create a new transaction record.</summary>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Type">Transaction type (Credit or Debit).</param>
/// <param name="Amount">Transaction amount (always positive).</param>
/// <param name="Fees">Transaction fees.</param>
/// <param name="Currency">Currency code (e.g. USD).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Gateway">Optional payment gateway name (e.g. Stripe).</param>
/// <param name="TransactionId">Optional external transaction ID.</param>
public sealed record CreateTransactionCommand(
    int ClientId,
    string Type,
    decimal Amount,
    decimal Fees,
    string Currency,
    string Description,
    string? Gateway = null,
    string? TransactionId = null);
