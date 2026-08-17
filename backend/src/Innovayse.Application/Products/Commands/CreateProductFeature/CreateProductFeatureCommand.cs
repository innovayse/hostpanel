namespace Innovayse.Application.Products.Commands.CreateProductFeature;

/// <summary>Adds one specification line to a product.</summary>
/// <param name="ProductId">Product the line describes.</param>
/// <param name="Label">Feature name; also the comparison table row heading.</param>
/// <param name="Value">Value shown for this product.</param>
/// <param name="SortOrder">Position within the product, ascending.</param>
public record CreateProductFeatureCommand(int ProductId, string Label, string Value, int SortOrder);
