namespace Innovayse.Application.Billing.DTOs;

/// <summary>DTO representing a single transaction returned by the API.</summary>
/// <param name="Id">Transaction primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Date">UTC timestamp of the transaction.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="TransactionId">External transaction reference.</param>
/// <param name="InvoiceId">Optional related invoice ID.</param>
/// <param name="PaymentMethod">Payment method used.</param>
/// <param name="AmountIn">Amount credited to the account.</param>
/// <param name="AmountOut">Amount debited from the account.</param>
/// <param name="Fees">Transaction fees charged.</param>
/// <param name="AddedToCredit">Whether this affected the client's credit balance.</param>
public record TransactionDto(
    int Id,
    int ClientId,
    string ClientName,
    DateTimeOffset Date,
    string Description,
    string TransactionId,
    int? InvoiceId,
    string PaymentMethod,
    decimal AmountIn,
    decimal AmountOut,
    decimal Fees,
    bool AddedToCredit);
