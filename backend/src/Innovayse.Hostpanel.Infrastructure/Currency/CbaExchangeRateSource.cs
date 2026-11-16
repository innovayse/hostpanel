using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>Exchange rates from the Central Bank of Armenia's daily bulletin.</summary>
/// <remarks>
/// <para>
/// <b>Endpoint and shape, as observed 2026-09-13.</b> <c>GET https://cb.am/latest.json.php</c>
/// answers <c>application/json</c> shaped
/// <c>{ "AED": "98.906", "ARS": null, "EUR": "421.11", "USD": "363.28" }</c>: one property per
/// currency the bank tracks, the value a <b>string</b> holding a decimal (not a number) meaning
/// "1 unit of this currency = X AMD", or <c>null</c> for a currency the bank lists but has no
/// quote for that day. The dram itself is not in the list; it is the unit everything is quoted in.
/// Precious metals (<c>XAU</c>, <c>XAG</c>) and the SDR appear alongside real currencies and are
/// harmless: nothing asks for them.
/// </para>
/// <para>
/// <b>Arithmetic.</b> Every quote is "AMD per unit", so with <c>amd(x)</c> the quote for <c>x</c>
/// and <c>amd(AMD) = 1</c>, "1 unit of <c>code</c> in <c>base</c>" is <c>amd(code) / amd(base)</c>,
/// worked out by <see cref="PivotRates"/>. With base USD and wanted AMD that is <c>1 / 363.28</c>;
/// with base AMD and wanted USD it is <c>363.28</c>; with base USD and wanted EUR it is
/// <c>421.11 / 363.28</c>. A base the bank does not quote cannot anchor any of this, and the call
/// refuses rather than guess.
/// </para>
/// <para>
/// <b>Unit amount.</b> The bank quotes a few currencies per 10 or per 100 units rather than per one
/// (its full bulletin carries an <c>Amount</c> beside each <c>Rate</c>). A quote may therefore also
/// arrive as an object <c>{ "rate": "2.43", "amount": 100 }</c>; the rate is divided by the amount,
/// so "100 units = 243 AMD" is read as "1 unit = 2.43 AMD" and never as a hundred times too much.
/// </para>
/// <para>
/// <b>Failures.</b> A bank that does not answer, answers with an error status, with a body that is
/// not the published object or with one larger than <see cref="ExchangeRateFeed.MaxBodyBytes"/> is
/// reported as <c>EXCHANGE_RATE_SOURCE_UNAVAILABLE</c> (503, with <c>Retry-After</c>), logged here
/// with the cause; the bank's own text never reaches the response. A base the bank does not quote is
/// <c>EXCHANGE_RATE_BASE_NOT_QUOTED</c>.
/// </para>
/// <para>
/// Registered as a typed client with the <c>Cba</c> read-only resilience profile (see
/// <see cref="Extensions.ExchangeRateHttpClientExtensions"/>): the one call is an idempotent GET the
/// daily job or an operator triggers, so a retry costs nothing and a bounded timeout keeps a slow
/// bank from holding the admin request open.
/// </para>
/// </remarks>
public sealed class CbaExchangeRateSource : IExchangeRateSource
{
    /// <summary>The bank's origin; the typed client registration sets it as the base address.</summary>
    public const string BaseAddress = "https://cb.am/";

    /// <summary>The daily bulletin, relative to <see cref="BaseAddress"/>.</summary>
    public const string LatestRatesPath = "latest.json.php";

    /// <summary>The currency every quote is expressed in, and the only one the bulletin omits.</summary>
    private const string PivotCode = "AMD";

    /// <summary>Property of an object-shaped quote holding the price of <see cref="AmountProperty"/> units.</summary>
    private const string RateProperty = "rate";

    /// <summary>Property of an object-shaped quote holding how many units the rate is for.</summary>
    private const string AmountProperty = "amount";

    /// <summary>The typed client, with the bank's origin as its base address.</summary>
    private readonly HttpClient _http;

    /// <summary>Records why a refresh failed; the response carries only the fixed code.</summary>
    private readonly ILogger<CbaExchangeRateSource> _logger;

    /// <summary>Initializes a new instance of the <see cref="CbaExchangeRateSource"/> class.</summary>
    /// <param name="http">The typed client, with the bank's origin as its base address.</param>
    /// <param name="logger">Records why a refresh failed; the response carries only the fixed code.</param>
    public CbaExchangeRateSource(HttpClient http, ILogger<CbaExchangeRateSource> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var quotes = await FetchAmdPerUnitAsync(ct);
        if (quotes is null)
        {
            return Result<ExchangeRateQuotes>.FailureCode(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
        }

        return PivotRates.Quote(lookup, PivotCode, quotes);
    }

    /// <summary>Downloads the bulletin and keeps only the properties that carry a usable quote.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// "AMD per one unit", keyed by upper-case code, unquoted (null) codes dropped; or <c>null</c>
    /// when the bank did not answer with the published object, the cause logged.
    /// </returns>
    private async Task<Dictionary<string, decimal>?> FetchAmdPerUnitAsync(CancellationToken ct)
    {
        var body = await ExchangeRateFeed.TryReadAsync(_http, LatestRatesPath, _logger, ct);
        if (body is null)
        {
            return null;
        }

        Dictionary<string, JsonElement>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The exchange rate bulletin was not the published object.");
            return null;
        }

        if (raw is null)
        {
            _logger.LogWarning("The exchange rate bulletin answered with an empty body.");
            return null;
        }

        var quotes = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (code, value) in raw)
        {
            var parsed = ParseQuote(value);
            if (parsed is not null)
            {
                quotes[code.ToUpperInvariant()] = parsed.Value;
            }
        }

        return quotes;
    }

    /// <summary>Reads one quote as "AMD per one unit", whichever of the shapes the bank has used for it.</summary>
    /// <remarks>
    /// The bulletin sends strings today; a number would be the more natural JSON and is accepted
    /// too, so a change on the bank's side does not turn every rate into "missing" overnight. An
    /// object carries the price of <c>amount</c> units, which is divided down to one.
    /// </remarks>
    /// <param name="value">The property's value.</param>
    /// <returns>The positive decimal per one unit, or <see langword="null"/> when there is none.</returns>
    private static decimal? ParseQuote(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return ParseDecimal(value);
        }

        var rate = TryGetPropertyIgnoringCase(value, RateProperty, out var rateValue) ? ParseDecimal(rateValue) : null;
        if (rate is null)
        {
            return null;
        }

        if (!TryGetPropertyIgnoringCase(value, AmountProperty, out var amountValue))
        {
            return rate;
        }

        var amount = ParseDecimal(amountValue);
        return amount is null ? null : rate / amount;
    }

    /// <summary>Reads a positive decimal held as a JSON number or as a string.</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The positive decimal, or <see langword="null"/> when there is none.</returns>
    private static decimal? ParseDecimal(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out var number) && number > 0 ? number : null,
            JsonValueKind.String => PivotRates.ParsePositive(value.GetString()),
            _ => null,
        };
    }

    /// <summary>Finds a property of an object without regard to the case of its name.</summary>
    /// <param name="value">The JSON object.</param>
    /// <param name="name">The property name.</param>
    /// <param name="property">The property's value when found.</param>
    /// <returns><see langword="true"/> when the object has the property.</returns>
    private static bool TryGetPropertyIgnoringCase(JsonElement value, string name, out JsonElement property)
    {
        foreach (var candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
