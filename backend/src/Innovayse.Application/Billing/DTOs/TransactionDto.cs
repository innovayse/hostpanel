namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a transaction in admin lists with client name.</summary>
/// <param name="Id">Transaction primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="InvoiceId">FK to the invoice if applicable.</param>
/// <param name="Type">Credit or Debit.</param>
/// <param name="Amount">Transaction amount.</param>
/// <param name="Fees">Transaction fees.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Gateway">Payment gateway name (or null).</param>
/// <param name="TransactionId">External transaction ID.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
public record TransactionDto(
    int Id,
    int ClientId,
    string ClientName,
    int? InvoiceId,
    string Type,
    decimal Amount,
    decimal Fees,
    string Currency,
    string? Gateway,
    string? TransactionId,
    string Description,
    DateTimeOffset CreatedAt);
