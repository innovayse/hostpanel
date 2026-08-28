namespace Innovayse.Application.Reports.DTOs;

/// <summary>One product row in the grouped Income by Product report.</summary>
/// <param name="ProductId">Identifier of the product.</param>
/// <param name="ProductName">Display name of the product.</param>
/// <param name="UnitsSold">Number of paid invoice lines for it in the month.</param>
/// <param name="TotalIncome">Income attributed to it in the month.</param>
public record IncomeByProductRowDto(
    int ProductId,
    string ProductName,
    int UnitsSold,
    decimal TotalIncome);
