namespace Innovayse.Hostpanel.Domain.Products.Enums;

/// <summary>How an option's price modifier is applied to the product's price.</summary>
public enum ProductOptionPriceType
{
    /// <summary>The modifier is an amount of money added to the price.</summary>
    Fixed = 0,

    /// <summary>The modifier is a percentage, 0 to 100, of the product's price.</summary>
    Percentage = 1,
}
