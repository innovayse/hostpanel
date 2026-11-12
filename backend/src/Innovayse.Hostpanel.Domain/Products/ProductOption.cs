using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// One choice inside a <see cref="ProductOptionGroup"/>, with the price it adds to the product.
/// Stored in the <c>ProductOptions</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Money convention: <see cref="PriceModifier"/> and <see cref="SetupFeeModifier"/> are stored
/// exactly as <see cref="ProductPrice.Amount"/> is, as <c>numeric(18,4)</c> and never negative.
/// A <see cref="ProductOptionPriceType.Fixed"/> amount is in the platform's base currency. An
/// option row carries no currency code of its own: a price in another currency is a later,
/// per-currency override row beside the option, the way a product's price is one row per
/// currency, so adding it changes no column here. A <see cref="ProductOptionPriceType.Percentage"/>
/// amount has no currency at all, it is a share of the product's own price in whichever currency
/// the order is billed in.
/// </para>
/// <para>
/// An option is created and changed only through its <see cref="ProductOptionGroup"/>, which
/// judges what the group's type allows.
/// </para>
/// </remarks>
public sealed class ProductOption : BaseEntity
{
    /// <summary>The longest option name, matching the <c>ProductOptions.Name</c> column.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Gets the FK to the <see cref="ProductOptionGroup"/> this choice belongs to.</summary>
    public int ProductOptionGroupId { get; private set; }

    /// <summary>Gets the name the client sees for this choice.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets what the choice adds to the recurring price, in the unit named by <see cref="PriceType"/>.</summary>
    public decimal PriceModifier { get; private set; }

    /// <summary>Gets whether <see cref="PriceModifier"/> and <see cref="SetupFeeModifier"/> are money or a percentage.</summary>
    public ProductOptionPriceType PriceType { get; private set; }

    /// <summary>Gets what the choice adds to the one-off setup fee, in the unit named by <see cref="PriceType"/>.</summary>
    public decimal SetupFeeModifier { get; private set; }

    /// <summary>Gets the position of this choice within its group, ascending.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProductOption() { }

    /// <summary>Creates a choice. Called by <see cref="ProductOptionGroup.AddOption"/> only.</summary>
    /// <param name="name">The name the client sees.</param>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="priceType">Whether the modifiers are money or a percentage.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    /// <param name="displayOrder">Position within the group, ascending.</param>
    /// <exception cref="ArgumentException">The name is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">A modifier is negative, or a percentage above 100.</exception>
    internal ProductOption(
        string name,
        decimal priceModifier,
        ProductOptionPriceType priceType,
        decimal setupFeeModifier,
        int displayOrder)
    {
        Apply(name, priceModifier, priceType, setupFeeModifier, displayOrder);
    }

    /// <summary>Replaces every editable field. Called by <see cref="ProductOptionGroup.UpdateOption"/> only.</summary>
    /// <param name="name">The name the client sees.</param>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="priceType">Whether the modifiers are money or a percentage.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    /// <param name="displayOrder">Position within the group, ascending.</param>
    /// <exception cref="ArgumentException">The name is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">A modifier is negative, or a percentage above 100.</exception>
    internal void Update(
        string name,
        decimal priceModifier,
        ProductOptionPriceType priceType,
        decimal setupFeeModifier,
        int displayOrder)
    {
        Apply(name, priceModifier, priceType, setupFeeModifier, displayOrder);
    }

    /// <summary>Checks every field, then writes them, so a refused change leaves the choice as it was.</summary>
    /// <param name="name">The name the client sees.</param>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="priceType">Whether the modifiers are money or a percentage.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    /// <param name="displayOrder">Position within the group, ascending.</param>
    private void Apply(
        string name,
        decimal priceModifier,
        ProductOptionPriceType priceType,
        decimal setupFeeModifier,
        int displayOrder)
    {
        var trimmed = ProductOptionRules.NormaliseName(name, NameMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        ProductOptionRules.EnsurePrice(priceModifier, priceType);
        ProductOptionRules.EnsurePrice(setupFeeModifier, priceType);

        Name = trimmed;
        PriceModifier = priceModifier;
        PriceType = priceType;
        SetupFeeModifier = setupFeeModifier;
        DisplayOrder = displayOrder;
    }
}
