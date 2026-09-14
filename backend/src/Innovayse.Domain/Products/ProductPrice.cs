namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;

/// <summary>
/// One stored price for a product: this much, in this currency, per this cycle.
/// </summary>
/// <remarks>
/// A row per currency is the whole point of multi-currency pricing. Nothing is derived from an
/// exchange rate when a client is shown or charged a price — an admin wrote this number.
/// </remarks>
public sealed class ProductPrice : Entity
{
    /// <summary>FK to the owning product.</summary>
    public int ProductId { get; private set; }

    /// <summary>ISO 4217 alpha code, upper case.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>The billing cycle this price is for.</summary>
    public BillingCycle Cycle { get; private set; }

    /// <summary>The amount, in <see cref="CurrencyCode"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ProductPrice() : base(0) { }

    /// <summary>Creates a price row. Called by <see cref="Product.SetPrice"/> only.</summary>
    /// <param name="currencyCode">Alpha code, normalised to upper case.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <param name="amount">The amount; must not be negative.</param>
    internal ProductPrice(string currencyCode, BillingCycle cycle, decimal amount) : base(0)
    {
        CurrencyCode = currencyCode.ToUpperInvariant();
        Cycle = cycle;
        Amount = amount;
    }

    /// <summary>Replaces the amount.</summary>
    /// <param name="amount">The new amount.</param>
    internal void SetAmount(decimal amount) => Amount = amount;
}
