namespace Innovayse.Application.Reports.Common;

/// <summary>Full Monthly Transactions report result.</summary>
public record MonthlyTransactionsReportDto(
    int Month,
    int Year,
    IReadOnlyList<DailyTransactionDto> Rows,
    decimal TotalAmountIn,
    decimal TotalFees,
    decimal TotalAmountOut);
