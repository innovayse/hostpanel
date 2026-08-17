namespace Innovayse.Application.Products.Queries.GetProductFeatures;

/// <summary>Represents one line of a product's specification.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">Product this line describes.</param>
/// <param name="Label">Feature name; also the comparison table row heading.</param>
/// <param name="Value">Value shown for this product.</param>
/// <param name="SortOrder">Position within the product, ascending.</param>
public record ProductFeatureDto(
    int Id,
    int ProductId,
    string Label,
    string Value,
    int SortOrder);
