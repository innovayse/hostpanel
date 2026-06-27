namespace Innovayse.Application.Domains.Queries.GetTldPricing;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Queries enabled TLD configurations from the database, converts sell prices
/// to the requested currency, and caches the DB results for performance.
/// </summary>
public sealed class GetTldPricingHandler(
    ITldConfigRepository tldConfigRepo,
    IMemoryCache cache,
    ILogger<GetTldPricingHandler> logger)
{
    /// <summary>Cache key for enabled TLD configurations loaded from the database.</summary>
    private const string CacheKey = "tld-pricing-configs";

    /// <summary>Duration to cache TLD configurations before refreshing from the database.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Approximate exchange rates from AMD to other currencies.
    /// Updated periodically — in production these should come from a rates API.
    /// </summary>
    private static readonly Dictionary<string, decimal> AmdRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AMD"] = 1m,
        ["USD"] = 1m / 390m,
        ["EUR"] = 1m / 420m,
        ["RUB"] = 1m / 4.5m,
        ["GBP"] = 1m / 490m,
    };

    /// <summary>
    /// Handles <see cref="GetTldPricingQuery"/> by returning cached or freshly-loaded TLD pricing,
    /// converted to the requested target currency.
    /// </summary>
    /// <param name="query">The TLD pricing query with optional target currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>TLD pricing data with currency information and per-TLD price breakdowns.</returns>
    public async Task<TldPricingDto> HandleAsync(GetTldPricingQuery query, CancellationToken ct)
    {
        if (!cache.TryGetValue(CacheKey, out List<TldConfig>? tldConfigs) || tldConfigs is null)
        {
            logger.LogInformation("Loading enabled TLD configs from database (cache miss)");
            tldConfigs = await tldConfigRepo.ListEnabledAsync(ct);
            cache.Set(CacheKey, tldConfigs, CacheDuration);
        }

        var targetCurrency = string.IsNullOrWhiteSpace(query.TargetCurrency) ? "USD" : query.TargetCurrency;

        return MapToDto(tldConfigs, targetCurrency);
    }

    /// <summary>
    /// Maps <see cref="TldConfig"/> entities to the API response DTO,
    /// converting sell prices from the TLD's sell currency to the target currency.
    /// </summary>
    /// <param name="configs">Enabled TLD configurations from the database.</param>
    /// <param name="targetCurrency">ISO 4217 currency code to convert to.</param>
    /// <returns>Formatted DTO ready for API serialization.</returns>
    private static TldPricingDto MapToDto(List<TldConfig> configs, string targetCurrency)
    {
        var prefix = targetCurrency.ToUpperInvariant() switch
        {
            "AMD" => "֏",
            "EUR" => "€",
            "GBP" => "£",
            "RUB" => "₽",
            _ => "$",
        };

        var pricing = new Dictionary<string, TldPriceEntryDto>();

        foreach (var config in configs)
        {
            var rate = GetConversionRate(config.SellCurrency, targetCurrency);

            var register = config.SellRegister.ToDictionary(
                kv => kv.Key.ToString(),
                kv => (kv.Value * rate).ToString("F2"));

            var transfer = config.SellTransfer.ToDictionary(
                kv => kv.Key.ToString(),
                kv => (kv.Value * rate).ToString("F2"));

            var renew = config.SellRenew.ToDictionary(
                kv => kv.Key.ToString(),
                kv => (kv.Value * rate).ToString("F2"));

            var categories = config.Categories.ToList();

            pricing[config.Tld] = new TldPriceEntryDto(register, transfer, renew, categories);
        }

        return new TldPricingDto(new TldCurrencyDto(targetCurrency.ToUpperInvariant(), prefix), pricing);
    }

    /// <summary>
    /// Calculates the conversion rate between two currencies via AMD as the pivot.
    /// </summary>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <returns>Multiplier to convert from source to target.</returns>
    private static decimal GetConversionRate(string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        // Convert source -> AMD -> target.
        // AmdRates stores: 1 AMD = X target. So to go from AMD to target, multiply.
        // To go from source to AMD: divide by AmdRates[source].
        var sourceToAmd = AmdRates.TryGetValue(fromCurrency, out var fromRate) ? 1m / fromRate : 1m;
        var amdToTarget = AmdRates.TryGetValue(toCurrency, out var toRate) ? toRate : 1m;

        return sourceToAmd * amdToTarget;
    }
}
