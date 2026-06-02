namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Income by Product report.</summary>
public record IncomeByProductDto(
    string Product,
    int UnitsSold,
    decimal TotalIncome,
    double Percentage);
