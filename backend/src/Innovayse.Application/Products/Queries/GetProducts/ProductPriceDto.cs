namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>A product's stored prices in one currency.</summary>
/// <param name="CurrencyCode">ISO 4217 alpha code.</param>
/// <param name="Monthly">Monthly price, or null when the product does not bill monthly in this currency.</param>
/// <param name="Annual">Annual price, or null when the product does not bill annually in this currency.</param>
public record ProductPriceDto(string CurrencyCode, decimal? Monthly, decimal? Annual);
