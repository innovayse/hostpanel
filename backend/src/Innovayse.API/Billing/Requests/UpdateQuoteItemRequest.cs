namespace Innovayse.API.Billing.Requests;

/// <summary>A single line item entry in an update-quote request.</summary>
public sealed class UpdateQuoteItemRequest
{
    /// <summary>Gets or initialises the existing item ID; null for new items.</summary>
    public int? Id { get; init; }

    /// <summary>Gets or initialises the human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets or initialises the discount percentage (0–100).</summary>
    public decimal DiscountPercent { get; init; }

    /// <summary>Gets or initialises whether this item is taxed.</summary>
    public bool Taxed { get; init; }

    /// <summary>Gets or initialises whether this item should be deleted.</summary>
    public bool IsDeleted { get; init; }
}
