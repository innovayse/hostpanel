namespace Innovayse.Application.Products.Common;

/// <summary>A product's prices in one currency, as an admin enters them.</summary>
/// <param name="CurrencyCode">ISO 4217 alpha code.</param>
/// <param name="Monthly">Monthly price, or null when the product does not bill monthly in this currency.</param>
/// <param name="Annual">Annual price, or null when the product does not bill annually in this currency.</param>
public sealed record ProductPriceInput(string CurrencyCode, decimal? Monthly, decimal? Annual);
