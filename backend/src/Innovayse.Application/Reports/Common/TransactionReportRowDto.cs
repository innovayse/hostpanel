namespace Innovayse.Application.Reports.Common;

/// <summary>One row of the Transactions report.</summary>
public record TransactionReportRowDto(
    int Id,
    int ClientId,
    string ClientName,
    string Currency,
    string PaymentMethod,
    string Date,
    string Description,
    int? InvoiceId,
    string TransactionId,
    decimal AmountIn,
    decimal Fees,
    decimal AmountOut);
