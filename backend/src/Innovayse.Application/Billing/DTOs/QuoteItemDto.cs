namespace Innovayse.Application.Billing.DTOs;

<<<<<<< HEAD
/// <summary>DTO for a quote line item.</summary>
/// <param name="Id">QuoteItem primary key.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="Amount">Total for this line item (UnitPrice * Quantity).</param>
public record QuoteItemDto(
    int Id,
    string Description,
    decimal UnitPrice,
    int Quantity,
=======
/// <summary>DTO representing a single quote line item.</summary>
/// <param name="Id">Quote item primary key.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="DiscountPercent">Discount percentage (0–100).</param>
/// <param name="Taxed">Whether the item is subject to tax.</param>
/// <param name="Amount">Computed line total after discount.</param>
public record QuoteItemDto(
    int Id,
    int Quantity,
    string Description,
    decimal UnitPrice,
    decimal DiscountPercent,
    bool Taxed,
>>>>>>> origin/main
    decimal Amount);
