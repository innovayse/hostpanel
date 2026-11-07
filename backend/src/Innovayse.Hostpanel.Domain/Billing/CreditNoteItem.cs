using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A line of a <see cref="CreditNote"/>: what is credited, before tax. Owned by the credit note; stored in the
/// <c>CreditNoteItems</c> table.
/// </summary>
public sealed class CreditNoteItem : BaseEntity
{
    /// <summary>Gets the FK to the parent <see cref="CreditNote"/>.</summary>
    public int CreditNoteId { get; private set; }

    /// <summary>Gets what is credited, in words.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the credited price per unit, before tax.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the number of units credited.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the line total before tax: <see cref="UnitPrice"/> times <see cref="Quantity"/>, rounded to the currency's minor unit.</summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private CreditNoteItem()
    {
    }

    /// <summary>Creates a credit note line.</summary>
    /// <param name="description">What is credited; not blank.</param>
    /// <param name="unitPrice">Price per unit before tax; greater than 0.</param>
    /// <param name="quantity">Number of units; at least 1.</param>
    /// <param name="currency">ISO 4217 code the amounts are in, for rounding the line total.</param>
    /// <returns>The new line.</returns>
    /// <exception cref="DomainRuleException"><c>CREDIT_NOTE_ITEMS_REQUIRED</c> when the description is blank, the price is not positive or the quantity is below 1.</exception>
    public static CreditNoteItem Create(string description, decimal unitPrice, int quantity, string currency)
    {
        if (string.IsNullOrWhiteSpace(description) || unitPrice <= 0 || quantity < 1)
        {
            throw new DomainRuleException(
                BillingErrors.CreditNoteItemsRequired, "A credit note line needs a description, a positive price and a quantity of at least 1.");
        }

        return new CreditNoteItem
        {
            Description = description.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            Amount = CurrencyCode.RoundToMinorUnits(unitPrice * quantity, currency),
        };
    }
}
