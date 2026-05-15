namespace Innovayse.Domain.Products;

/// <summary>Lifecycle status of a product listing.</summary>
public enum ProductStatus
{
    /// <summary>Product is available for ordering.</summary>
    Active,

    /// <summary>Product is hidden and cannot be ordered.</summary>
    Inactive,
}
