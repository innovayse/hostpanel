namespace Innovayse.Application.Domains.DTOs;

/// <summary>Top-level response containing TLD pricing data and currency information.</summary>
/// <param name="Currency">Currency information for the pricing values.</param>
/// <param name="Pricing">Dictionary keyed by TLD extension (e.g. "com") with pricing details.</param>
public record TldPricingDto(
    TldCurrencyDto Currency,
    Dictionary<string, TldPriceEntryDto> Pricing);

/// <summary>Currency metadata for TLD pricing.</summary>
/// <param name="Code">ISO 4217 currency code (e.g. "USD").</param>
/// <param name="Prefix">Currency symbol prefix (e.g. "$").</param>
public record TldCurrencyDto(string Code, string Prefix);

/// <summary>Pricing details for a single TLD extension.</summary>
/// <param name="Register">Registration prices keyed by period in years (e.g. "1" => "9.99").</param>
/// <param name="Transfer">Transfer prices keyed by period in years.</param>
/// <param name="Renew">Renewal prices keyed by period in years.</param>
/// <param name="Categories">Category tags for filtering (e.g. "Popular", "Country").</param>
public record TldPriceEntryDto(
    Dictionary<string, string> Register,
    Dictionary<string, string> Transfer,
    Dictionary<string, string> Renew,
    List<string> Categories);
