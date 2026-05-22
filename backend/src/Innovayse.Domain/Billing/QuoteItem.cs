namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A single line item on a <see cref="Quote"/>.
/// Owned by the Quote aggregate; stored in the <c>quote_items</c> table.
/// </summary>
public sealed class QuoteItem : Entity
{
    /// <summary>Gets the FK to the parent <see cref="Quote"/> (set by EF after save).</summary>
    public int QuoteId { get; private set; }

    /// <summary>Gets the number of units.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the human-readable description of the charge.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the discount percentage applied to this item (0–100).</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>Gets a value indicating whether this item is subject to tax.</summary>
    public bool Taxed { get; private set; }

    /// <summary>
    /// Gets the computed line total: <c>Quantity * UnitPrice * (1 - DiscountPercent / 100)</c>.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private QuoteItem() : base(0) { }

    /// <summary>
    /// Creates a new quote line item.
    /// </summary>
    /// <param name="quantity">Number of units (>= 1).</param>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (>= 0).</param>
    /// <param name="discountPercent">Discount percentage (0–100).</param>
    /// <param name="taxed">Whether this item is subject to tax.</param>
    /// <returns>A new <see cref="QuoteItem"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative, <paramref name="quantity"/> is less than 1, or <paramref name="discountPercent"/> is outside 0–100.</exception>
    public static QuoteItem Create(int quantity, string description, decimal unitPrice, decimal discountPercent, bool taxed)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be >= 0.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount must be between 0 and 100.");
        }

        return new()
        {
            Quantity = quantity,
            Description = description,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            Taxed = taxed,
            Amount = quantity * unitPrice * (1 - discountPercent / 100m),
        };
    }

    /// <summary>
    /// Updates the line item properties and recalculates <see cref="Amount"/>.
    /// </summary>
    /// <param name="quantity">New number of units (>= 1).</param>
    /// <param name="description">New human-readable charge description.</param>
    /// <param name="unitPrice">New price per unit (>= 0).</param>
    /// <param name="discountPercent">New discount percentage (0–100).</param>
    /// <param name="taxed">Whether this item is subject to tax.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative, <paramref name="quantity"/> is less than 1, or <paramref name="discountPercent"/> is outside 0–100.</exception>
    public void Update(int quantity, string description, decimal unitPrice, decimal discountPercent, bool taxed)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be >= 0.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount must be between 0 and 100.");
        }

        Quantity = quantity;
        Description = description;
        UnitPrice = unitPrice;
        DiscountPercent = discountPercent;
        Taxed = taxed;
        Amount = quantity * unitPrice * (1 - discountPercent / 100m);
    }
}
