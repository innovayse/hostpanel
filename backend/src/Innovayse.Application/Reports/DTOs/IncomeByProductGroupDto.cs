namespace Innovayse.Application.Reports.DTOs;

/// <summary>One product group in the grouped Income by Product report.</summary>
/// <param name="GroupName">Name of the product group.</param>
/// <param name="Products">Products belonging to the group.</param>
/// <param name="GroupUnitsSold">Units sold across the whole group.</param>
/// <param name="GroupIncome">Income across the whole group.</param>
public record IncomeByProductGroupDto(
    string GroupName,
    IReadOnlyList<IncomeByProductRowDto> Products,
    int GroupUnitsSold,
    decimal GroupIncome);
