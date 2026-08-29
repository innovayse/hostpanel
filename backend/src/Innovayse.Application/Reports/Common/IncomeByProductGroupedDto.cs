namespace Innovayse.Application.Reports.Common;

/// <summary>Full grouped Income by Product report result.</summary>
/// <param name="Month">Calendar month the report covers, 1-12.</param>
/// <param name="Year">Calendar year the report covers.</param>
/// <param name="Groups">Income grouped by product group.</param>
/// <param name="TotalUnitsSold">Units sold across every group.</param>
/// <param name="TotalIncome">Income across every group.</param>
public record IncomeByProductGroupedDto(
    int Month,
    int Year,
    IReadOnlyList<IncomeByProductGroupDto> Groups,
    int TotalUnitsSold,
    decimal TotalIncome);
