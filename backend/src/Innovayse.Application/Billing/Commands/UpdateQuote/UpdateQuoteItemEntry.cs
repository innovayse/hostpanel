namespace Innovayse.Application.Billing.Commands.UpdateQuote;

/// <summary>
/// A single line item entry for updating a quote.
/// When <paramref name="Id"/> is null, a new item is created.
/// When <paramref name="IsDeleted"/> is true, the item is removed.
/// </summary>
/// <param name="Id">Existing item ID; null for new items.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit (>= 0).</param>
/// <param name="Quantity">Number of units (>= 1).</param>
/// <param name="DiscountPercent">Discount percentage (0–100).</param>
/// <param name="Taxed">Whether this item is taxed.</param>
/// <param name="IsDeleted">When true, the item with this ID will be removed.</param>
public record UpdateQuoteItemEntry(
    int? Id,
    string Description,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountPercent = 0,
    bool Taxed = false,
    bool IsDeleted = false);
