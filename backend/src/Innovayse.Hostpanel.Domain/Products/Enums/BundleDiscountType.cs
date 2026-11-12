namespace Innovayse.Hostpanel.Domain.Products.Enums;

/// <summary>How a <see cref="ProductBundle"/> discounts the sum of its components.</summary>
public enum BundleDiscountType
{
    /// <summary>No discount: the bundle costs what its components cost.</summary>
    None = 0,

    /// <summary>A percentage of the sum, from 0 to 100.</summary>
    Percentage = 1,

    /// <summary>A fixed amount in the bundle's discount currency.</summary>
    Fixed = 2,
}
