namespace Innovayse.Domain.Billing;

<<<<<<< HEAD
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
=======
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
>>>>>>> origin/main

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

<<<<<<< HEAD
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

    /// <summary>Updates the item's details in place.</summary>
    public void Update(string description, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (quantity < 1) throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));
        Description = description;
        UnitPrice = unitPrice;
        Quantity = quantity;
=======
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
>>>>>>> origin/main
    }
}
