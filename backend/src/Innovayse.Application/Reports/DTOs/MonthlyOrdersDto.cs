namespace Innovayse.Application.Reports.DTOs;

/// <summary>Full Monthly Orders report result.</summary>
/// <param name="Month">Calendar month the report covers, 1-12.</param>
/// <param name="Year">Calendar year the report covers.</param>
/// <param name="Groups">Orders grouped by product group.</param>
/// <param name="TotalUnitsSold">Units sold across every group.</param>
/// <param name="TotalValue">Order value across every group.</param>
public record MonthlyOrdersDto(
    int Month,
    int Year,
    IReadOnlyList<MonthlyOrderGroupDto> Groups,
    int TotalUnitsSold,
    decimal TotalValue);
