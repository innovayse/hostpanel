namespace Innovayse.Application.Reports.Common;

/// <summary>One day row in the Monthly Transactions report.</summary>
public record DailyTransactionDto(
    string Date,
    decimal AmountIn,
    decimal Fees,
    decimal AmountOut,
    decimal Balance);
