namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A line item within a quote, representing a product or service being quoted.
/// Owned by <see cref="Quote"/>, stored in the <c>QuoteItems</c> table.
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

    /// <summary>Gets the discount percentage (0 to 100).</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>Gets a value indicating whether this item is taxed.</summary>
    public bool Taxed { get; private set; }

    /// <summary>Gets the calculated total for this line item after discount, unrounded: use <see cref="AmountIn"/> for an amount a payer can be charged.</summary>
    public decimal Amount => UnitPrice * Quantity * (1 - DiscountPercent / 100);

    /// <summary>Returns the line total after discount rounded to the minor unit of <paramref name="currency"/>, the same way an invoice rounds its amounts.</summary>
    /// <param name="currency">ISO 4217 alpha code the quote's amounts are in.</param>
    /// <returns>The discounted line total in major units, with no more decimals than the currency allows.</returns>
    public decimal AmountIn(string currency) => CurrencyCode.RoundToMinorUnits(Amount, currency);

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private QuoteItem() { }

    /// <summary>
    /// Creates a new quote item.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <param name="discountPercent">Discount percentage (0 to 100). Defaults to 0.</param>
    /// <param name="taxed">Whether this item is taxed. Defaults to false.</param>
    /// <returns>A new quote item.</returns>
    public static QuoteItem Create(string description, decimal unitPrice, int quantity, decimal discountPercent = 0, bool taxed = false)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        }

        if (quantity < 1)
        {
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));
        }

        return new QuoteItem
        {
            Description = description,
            UnitPrice = unitPrice,
            Quantity = quantity,
            DiscountPercent = discountPercent,
            Taxed = taxed,
        };
    }

    /// <summary>Updates the item's details in place.</summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <param name="discountPercent">Discount percentage (0 to 100).</param>
    /// <param name="taxed">Whether this item is taxed.</param>
    public void Update(string description, decimal unitPrice, int quantity, decimal discountPercent = 0, bool taxed = false)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        }

        if (quantity < 1)
        {
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));
        }

        Description = description;
        UnitPrice = unitPrice;
        Quantity = quantity;
        DiscountPercent = discountPercent;
        Taxed = taxed;
    }
}
