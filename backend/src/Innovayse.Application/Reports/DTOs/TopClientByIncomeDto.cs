namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Top 10 Clients by Income (transaction-based) report.</summary>
public record TopClientByIncomeDto(
    int ClientId,
    string ClientName,
    decimal TotalAmountIn,
    decimal TotalFees,
    decimal TotalAmountOut,
    decimal Balance);
