namespace Innovayse.Application.Billing.DTOs;

/// <summary>DTO for a quote line item.</summary>
/// <param name="Id">QuoteItem primary key.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="DiscountPercent">Discount percentage (0–100).</param>
/// <param name="Taxed">Whether this item is taxed.</param>
/// <param name="Amount">Total for this line item after discount.</param>
public record QuoteItemDto(
    int Id,
    string Description,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountPercent,
    bool Taxed,
    decimal Amount);
