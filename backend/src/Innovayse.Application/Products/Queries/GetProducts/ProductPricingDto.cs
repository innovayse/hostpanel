namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>Pricing details for a product.</summary>
/// <param name="Monthly">Monthly price.</param>
/// <param name="Annual">Annual price.</param>
public record ProductPricingDto(decimal Monthly, decimal Annual);
