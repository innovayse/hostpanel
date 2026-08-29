namespace Innovayse.Application.Reports.Common;

/// <summary>One product group in the Monthly Orders report.</summary>
/// <param name="GroupName">Name of the product group.</param>
/// <param name="Products">Products belonging to the group.</param>
/// <param name="GroupUnitsSold">Units sold across the whole group.</param>
/// <param name="GroupValue">Order value across the whole group.</param>
public record MonthlyOrderGroupDto(
    string GroupName,
    IReadOnlyList<MonthlyOrderProductDto> Products,
    int GroupUnitsSold,
    decimal GroupValue);
