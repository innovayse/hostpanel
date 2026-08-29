namespace Innovayse.Application.Reports.Common;

/// <summary>One product row in the Monthly Orders report.</summary>
/// <param name="ProductId">Identifier of the ordered product.</param>
/// <param name="ProductName">Display name of the product.</param>
/// <param name="UnitsSold">Number of orders placed for it in the month.</param>
/// <param name="Value">Total order value for it in the month.</param>
public record MonthlyOrderProductDto(
    int ProductId,
    string ProductName,
    int UnitsSold,
    decimal Value);
