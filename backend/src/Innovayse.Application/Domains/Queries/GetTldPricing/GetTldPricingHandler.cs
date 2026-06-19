namespace Innovayse.Application.Domains.Queries.GetTldPricing;

using Innovayse.Application.Domains.DTOs;

/// <summary>
/// Returns hardcoded TLD pricing data. Will be replaced with registrar API integration
/// once a pricing provider is connected.
/// </summary>
public sealed class GetTldPricingHandler
{
    /// <summary>
    /// Handles <see cref="GetTldPricingQuery"/> by returning a static set of TLD prices.
    /// </summary>
    /// <param name="query">The TLD pricing query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>TLD pricing data with currency information and per-TLD price breakdowns.</returns>
    public Task<TldPricingDto> HandleAsync(GetTldPricingQuery query, CancellationToken ct)
    {
        var currency = new TldCurrencyDto("USD", "$");

        var pricing = new Dictionary<string, TldPriceEntryDto>
        {
            ["com"] = new(
                Register: new Dictionary<string, string> { ["1"] = "12.99", ["2"] = "25.98", ["3"] = "38.97" },
                Transfer: new Dictionary<string, string> { ["1"] = "12.99" },
                Renew: new Dictionary<string, string> { ["1"] = "14.99", ["2"] = "29.98", ["3"] = "44.97" },
                Categories: ["Popular", "Business"]),

            ["net"] = new(
                Register: new Dictionary<string, string> { ["1"] = "14.99", ["2"] = "29.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "14.99" },
                Renew: new Dictionary<string, string> { ["1"] = "16.99", ["2"] = "33.98" },
                Categories: ["Popular", "Business"]),

            ["org"] = new(
                Register: new Dictionary<string, string> { ["1"] = "13.99", ["2"] = "27.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "13.99" },
                Renew: new Dictionary<string, string> { ["1"] = "15.99", ["2"] = "31.98" },
                Categories: ["Popular", "Non-Profit"]),

            ["am"] = new(
                Register: new Dictionary<string, string> { ["1"] = "49.99", ["2"] = "99.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "49.99" },
                Renew: new Dictionary<string, string> { ["1"] = "49.99", ["2"] = "99.98" },
                Categories: ["Country", "Armenia"]),

            ["co.am"] = new(
                Register: new Dictionary<string, string> { ["1"] = "29.99", ["2"] = "59.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "29.99" },
                Renew: new Dictionary<string, string> { ["1"] = "29.99", ["2"] = "59.98" },
                Categories: ["Country", "Armenia"]),

            ["io"] = new(
                Register: new Dictionary<string, string> { ["1"] = "39.99", ["2"] = "79.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "39.99" },
                Renew: new Dictionary<string, string> { ["1"] = "44.99", ["2"] = "89.98" },
                Categories: ["Popular", "Technology"]),

            ["dev"] = new(
                Register: new Dictionary<string, string> { ["1"] = "15.99", ["2"] = "31.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "15.99" },
                Renew: new Dictionary<string, string> { ["1"] = "17.99", ["2"] = "35.98" },
                Categories: ["Technology"]),

            ["co"] = new(
                Register: new Dictionary<string, string> { ["1"] = "29.99", ["2"] = "59.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "29.99" },
                Renew: new Dictionary<string, string> { ["1"] = "34.99", ["2"] = "69.98" },
                Categories: ["Popular", "Business"]),

            ["info"] = new(
                Register: new Dictionary<string, string> { ["1"] = "9.99", ["2"] = "19.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "9.99" },
                Renew: new Dictionary<string, string> { ["1"] = "12.99", ["2"] = "25.98" },
                Categories: ["General"]),

            ["biz"] = new(
                Register: new Dictionary<string, string> { ["1"] = "14.99", ["2"] = "29.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "14.99" },
                Renew: new Dictionary<string, string> { ["1"] = "16.99", ["2"] = "33.98" },
                Categories: ["Business"]),

            ["me"] = new(
                Register: new Dictionary<string, string> { ["1"] = "19.99", ["2"] = "39.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "19.99" },
                Renew: new Dictionary<string, string> { ["1"] = "22.99", ["2"] = "45.98" },
                Categories: ["Personal"]),

            ["xyz"] = new(
                Register: new Dictionary<string, string> { ["1"] = "2.99", ["2"] = "5.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "9.99" },
                Renew: new Dictionary<string, string> { ["1"] = "12.99", ["2"] = "25.98" },
                Categories: ["Popular", "General"]),

            ["online"] = new(
                Register: new Dictionary<string, string> { ["1"] = "4.99", ["2"] = "9.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "29.99" },
                Renew: new Dictionary<string, string> { ["1"] = "34.99", ["2"] = "69.98" },
                Categories: ["Business", "Technology"]),

            ["store"] = new(
                Register: new Dictionary<string, string> { ["1"] = "5.99", ["2"] = "11.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "49.99" },
                Renew: new Dictionary<string, string> { ["1"] = "54.99", ["2"] = "109.98" },
                Categories: ["Business", "E-Commerce"]),

            ["tech"] = new(
                Register: new Dictionary<string, string> { ["1"] = "6.99", ["2"] = "13.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "44.99" },
                Renew: new Dictionary<string, string> { ["1"] = "49.99", ["2"] = "99.98" },
                Categories: ["Technology"]),

            ["app"] = new(
                Register: new Dictionary<string, string> { ["1"] = "17.99", ["2"] = "35.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "17.99" },
                Renew: new Dictionary<string, string> { ["1"] = "19.99", ["2"] = "39.98" },
                Categories: ["Technology"]),

            ["cloud"] = new(
                Register: new Dictionary<string, string> { ["1"] = "12.99", ["2"] = "25.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "12.99" },
                Renew: new Dictionary<string, string> { ["1"] = "24.99", ["2"] = "49.98" },
                Categories: ["Technology"]),

            ["site"] = new(
                Register: new Dictionary<string, string> { ["1"] = "3.99", ["2"] = "7.98" },
                Transfer: new Dictionary<string, string> { ["1"] = "29.99" },
                Renew: new Dictionary<string, string> { ["1"] = "34.99", ["2"] = "69.98" },
                Categories: ["General"]),
        };

        var result = new TldPricingDto(currency, pricing);
        return Task.FromResult(result);
    }
}
