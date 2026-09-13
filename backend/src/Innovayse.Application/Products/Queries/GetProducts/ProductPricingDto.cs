namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>
/// Pricing details for a product in the caller's currency.
/// </summary>
/// <remarks>
/// Kept so the storefront keeps rendering a number until it reads <see cref="ProductDto.Prices"/>
/// instead. Filled from the currency the caller is billed in; a value is <see langword="null"/>
/// when the product has no price for that cycle in that currency.
/// </remarks>
/// <param name="Monthly">Monthly price in the caller's currency, or null.</param>
/// <param name="Annual">Annual price in the caller's currency, or null.</param>
public record ProductPricingDto(decimal? Monthly, decimal? Annual);
