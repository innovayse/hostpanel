namespace Innovayse.Application.Reports.DTOs;

/// <summary>One product row in the grouped Income by Product report.</summary>
public record IncomeByProductRowDto(
    int ProductId,
    string ProductName,
    int UnitsSold,
    decimal TotalIncome);

/// <summary>One product group in the grouped Income by Product report.</summary>
public record IncomeByProductGroupDto(
    string GroupName,
    IReadOnlyList<IncomeByProductRowDto> Products,
    int GroupUnitsSold,
    decimal GroupIncome);

/// <summary>Full grouped Income by Product report result.</summary>
public record IncomeByProductGroupedDto(
    int Month,
    int Year,
    IReadOnlyList<IncomeByProductGroupDto> Groups,
    int TotalUnitsSold,
    decimal TotalIncome);
