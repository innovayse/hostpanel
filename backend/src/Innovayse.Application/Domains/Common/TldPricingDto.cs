namespace Innovayse.Application.Domains.Common;

/// <summary>Top-level response containing TLD pricing data and currency information.</summary>
/// <param name="Currency">Currency information for the pricing values.</param>
/// <param name="Pricing">Dictionary keyed by TLD extension (e.g. "com") with pricing details.</param>
public record TldPricingDto(
    TldCurrencyDto Currency,
    Dictionary<string, TldPriceEntryDto> Pricing);
