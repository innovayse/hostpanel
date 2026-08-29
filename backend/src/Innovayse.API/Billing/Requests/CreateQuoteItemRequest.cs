namespace Innovayse.API.Billing.Requests;

/// <summary>Request for a single quote line item.</summary>
public sealed class CreateQuoteItemRequest
{
    /// <summary>Gets or sets the human-readable description.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Gets or sets the price per unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets the discount percentage (0–100).</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Gets or sets whether this item is taxed.</summary>
    public bool Taxed { get; set; }
}
