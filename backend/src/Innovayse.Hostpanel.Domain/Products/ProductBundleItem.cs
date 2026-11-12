using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// One component of a <see cref="ProductBundle"/>: a catalogue product sold as part of the bundle.
/// Stored in the <c>ProductBundleItems</c> table.
/// </summary>
/// <remarks>
/// The bundle carries no quantity per component: an order line is one service, so a quantity
/// would have nothing to count.
/// </remarks>
public sealed class ProductBundleItem : BaseEntity
{
    /// <summary>Gets the FK to the <see cref="ProductBundle"/> this component belongs to.</summary>
    public int BundleId { get; private set; }

    /// <summary>Gets the FK to the <see cref="Product"/> sold as this component.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets whether this is the bundle's primary component: the one the others hang off (the hosting plan a domain is attached to).</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProductBundleItem() { }

    /// <summary>Creates a component for a bundle; the bundle's key is filled in when the bundle is stored.</summary>
    /// <param name="productId">FK to the product sold as this component.</param>
    /// <param name="isPrimary">Whether this is the bundle's primary component.</param>
    /// <returns>A new <see cref="ProductBundleItem"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The product id is not positive.</exception>
    public static ProductBundleItem Create(int productId, bool isPrimary)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        return new ProductBundleItem { ProductId = productId, IsPrimary = isPrimary };
    }

    /// <summary>Marks or unmarks this component as the bundle's primary one.</summary>
    /// <param name="isPrimary">Whether this is the primary component.</param>
    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
