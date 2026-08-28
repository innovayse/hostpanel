namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>One payment record, as the <c>transactions</c> section of a client data export lists it.</summary>
/// <param name="Id">The transaction's primary key.</param>
/// <param name="Date">When the money moved.</param>
/// <param name="Description">What the payment was for.</param>
/// <param name="AmountIn">Amount received from the client.</param>
/// <param name="AmountOut">Amount refunded or paid back to the client.</param>
/// <param name="Fees">Gateway fees deducted from the amount received.</param>
/// <param name="PaymentMethod">Gateway module the payment went through.</param>
public sealed record ClientExportTransactionDto(
    int Id,
    DateTimeOffset Date,
    string Description,
    decimal AmountIn,
    decimal AmountOut,
    decimal Fees,
    string PaymentMethod);
