namespace Innovayse.Application.Products.Commands.UpdateProductFeature;

/// <summary>Updates one specification line.</summary>
/// <param name="Id">Feature line primary key.</param>
/// <param name="Label">Feature name; also the comparison table row heading.</param>
/// <param name="Value">Value shown for this product.</param>
/// <param name="SortOrder">Position within the product, ascending.</param>
public record UpdateProductFeatureCommand(int Id, string Label, string Value, int SortOrder);
