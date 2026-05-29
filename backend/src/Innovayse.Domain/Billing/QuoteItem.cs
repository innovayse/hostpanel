namespace Innovayse.Domain.Billing;

/// <summary>
/// A line item within a quote, representing a product or service being quoted.
/// Owned by <see cref="Quote"/>, stored in the <c>quote_items</c> table.
/// </summary>
public sealed class QuoteItem
{
    /// <summary>Gets the primary key.</summary>
    public int Id { get; private set; }

    /// <summary>Gets the FK to the owning quote.</summary>
    public int QuoteId { get; private set; }

    /// <summary>Gets a human-readable description of the quoted item.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the quantity (number of units).</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the calculated total for this line item (UnitPrice * Quantity).</summary>
    public decimal Amount => UnitPrice * Quantity;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private QuoteItem() { }

    /// <summary>
    /// Creates a new quote item.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <returns>A new quote item.</returns>
    public static QuoteItem Create(string description, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (quantity < 1) throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));

        return new QuoteItem
        {
            Description = description,
            UnitPrice = unitPrice,
            Quantity = quantity,
        };
    }
}
