namespace Innovayse.Application.Reports.DTOs;

/// <summary>One product row in the Monthly Orders report.</summary>
public record MonthlyOrderProductDto(
    int ProductId,
    string ProductName,
    int UnitsSold,
    decimal Value);

/// <summary>One product group in the Monthly Orders report.</summary>
public record MonthlyOrderGroupDto(
    string GroupName,
    IReadOnlyList<MonthlyOrderProductDto> Products,
    int GroupUnitsSold,
    decimal GroupValue);

/// <summary>Full Monthly Orders report result.</summary>
public record MonthlyOrdersDto(
    int Month,
    int Year,
    IReadOnlyList<MonthlyOrderGroupDto> Groups,
    int TotalUnitsSold,
    decimal TotalValue);
