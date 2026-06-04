namespace Innovayse.Application.Reports.DTOs;

/// <summary>One day row in the Monthly Transactions report.</summary>
public record DailyTransactionDto(
    string Date,
    decimal AmountIn,
    decimal Fees,
    decimal AmountOut,
    decimal Balance);

/// <summary>Full Monthly Transactions report result.</summary>
public record MonthlyTransactionsReportDto(
    int Month,
    int Year,
    IReadOnlyList<DailyTransactionDto> Rows,
    decimal TotalAmountIn,
    decimal TotalFees,
    decimal TotalAmountOut);
