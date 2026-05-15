namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A single line item on an <see cref="Invoice"/>.
/// Owned by the Invoice aggregate; stored in the <c>invoice_items</c> table.
/// </summary>
public sealed class InvoiceItem : Entity
{
    /// <summary>Gets the FK to the parent <see cref="Invoice"/> (set by EF after save).</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets the human-readable description of the charge.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the number of units.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the line total (<see cref="UnitPrice"/> × <see cref="Quantity"/>).</summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private InvoiceItem() : base(0) { }

    /// <summary>
    /// Creates a new invoice line item.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <returns>A new <see cref="InvoiceItem"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative or <paramref name="quantity"/> is less than 1.</exception>
    public static InvoiceItem Create(string description, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be >= 0.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1.");
        }

        return new()
        {
            Description = description,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Amount = unitPrice * quantity,
        };
    }
}
