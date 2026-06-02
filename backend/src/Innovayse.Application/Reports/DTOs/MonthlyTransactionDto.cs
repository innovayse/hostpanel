namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Monthly Transactions report.</summary>
public record MonthlyTransactionDto(
    string Month,
    int Count,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalFees);
