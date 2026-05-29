namespace Innovayse.Application.Billing.Commands.UpdateTransaction;

/// <summary>
/// Updates an existing transaction record.
/// </summary>
/// <param name="Id">Transaction ID.</param>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Type">Credit or Debit.</param>
/// <param name="Amount">Transaction amount.</param>
/// <param name="Fees">Transaction fees.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Gateway">Optional gateway name.</param>
/// <param name="TransactionId">Optional external transaction ID.</param>
public sealed record UpdateTransactionCommand(
    int Id,
    int ClientId,
    string Type,
    decimal Amount,
    decimal Fees,
    string Currency,
    string Description,
    string? Gateway = null,
    string? TransactionId = null);
