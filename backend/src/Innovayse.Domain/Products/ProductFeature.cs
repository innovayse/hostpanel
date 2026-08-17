namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;

/// <summary>
/// One line of a product's specification, such as "Disk" / "10 GB".
/// Belongs to a <see cref="Product"/>.
/// Stored in the <c>product_features</c> table.
/// </summary>
/// <remarks>
/// <para>
/// The storefront's comparison table is built by collecting every distinct
/// <see cref="Label"/> across the products on show and filling in each product's
/// value, so a product that does not carry a given line simply reads as absent
/// rather than breaking the table's shape.
/// </para>
/// <para>
/// That makes the label the thing that has to match between products, and it is
/// stored per product rather than in a shared catalogue: two products spelling
/// the same feature differently produce two rows. The trade is deliberate — it
/// keeps a product's specification editable in one place.
/// </para>
/// </remarks>
public sealed class ProductFeature : Entity
{
    /// <summary>Gets the FK to the <see cref="Product"/> this line describes.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the feature name, which is also the comparison table's row heading.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Gets the value shown for this product, e.g. "10 GB", "Unlimited" or "Yes".</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets the position of this line within the product, ascending.</summary>
    public int SortOrder { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ProductFeature() : base(0) { }

    /// <summary>
    /// Creates a feature line for a product.
    /// </summary>
    /// <param name="productId">FK to the product being described.</param>
    /// <param name="label">Feature name; also the comparison table row heading.</param>
    /// <param name="value">Value shown for this product.</param>
    /// <param name="sortOrder">Position within the product, ascending.</param>
    /// <returns>A new <see cref="ProductFeature"/>.</returns>
    /// <exception cref="ArgumentException">Label or value is blank.</exception>
    public static ProductFeature Create(int productId, string label, string value, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new ProductFeature
        {
            ProductId = productId,
            Label = label.Trim(),
            Value = value.Trim(),
            SortOrder = sortOrder,
        };
    }

    /// <summary>
    /// Updates this line in place.
    /// </summary>
    /// <param name="label">Feature name; also the comparison table row heading.</param>
    /// <param name="value">Value shown for this product.</param>
    /// <param name="sortOrder">Position within the product, ascending.</param>
    /// <exception cref="ArgumentException">Label or value is blank.</exception>
    public void Update(string label, string value, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Label = label.Trim();
        Value = value.Trim();
        SortOrder = sortOrder;
    }
}
