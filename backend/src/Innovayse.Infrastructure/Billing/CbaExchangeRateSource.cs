namespace Innovayse.Infrastructure.Billing;

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Application.Billing.Interfaces;

/// <summary>Exchange rates from the Central Bank of Armenia's daily bulletin.</summary>
/// <remarks>
/// <para>
/// <b>Endpoint and shape, as observed 2026-09-13.</b> <c>GET https://cb.am/latest.json.php</c>
/// answers <c>application/json</c> shaped
/// <c>{ "AED": "98.906", "ARS": null, "EUR": "421.11", "USD": "363.28", ... }</c>: one property per
/// currency the bank tracks, the value a <b>string</b> holding a decimal (not a number) meaning
/// "1 unit of this currency = X AMD", or <c>null</c> for a currency the bank lists but has no
/// quote for that day. The dram itself is not in the list; it is the unit everything is quoted in.
/// Precious metals (<c>XAU</c>, <c>XAG</c>) and the SDR appear alongside real currencies and are
/// harmless: nothing asks for them.
/// </para>
/// <para>
/// <b>Arithmetic.</b> Every quote is "AMD per unit", so with <c>amd(x)</c> the quote for <c>x</c>
/// and <c>amd(AMD) = 1</c>, "1 unit of <c>code</c> in <c>base</c>" is
/// <c>amd(code) / amd(base)</c>. With base USD and wanted AMD that is <c>1 / 363.28</c>; with base
/// AMD and wanted USD it is <c>363.28</c>; with base USD and wanted EUR it is
/// <c>421.11 / 363.28</c>. A base the bank does not quote cannot anchor any of this, and the call
/// refuses rather than guess.
/// </para>
/// <para>
/// Registered as a typed client with the <c>Cba</c> read-only resilience profile: the one call is
/// an idempotent GET an operator triggers by hand, so a retry costs nothing and a bounded timeout
/// keeps a slow bank from holding the admin request open.
/// </para>
/// </remarks>
/// <param name="http">The typed client, with the bank's origin as its base address.</param>
public sealed class CbaExchangeRateSource(HttpClient http) : IExchangeRateSource
{
    /// <summary>The bank's origin; the typed client registration sets it as the base address.</summary>
    public const string BaseAddress = "https://cb.am/";

    /// <summary>The daily bulletin, relative to <see cref="BaseAddress"/>.</summary>
    public const string LatestRatesPath = "latest.json.php";

    /// <summary>The currency every quote is expressed in, and the only one the bulletin omits.</summary>
    private const string PivotCode = "AMD";

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, decimal>> RatesToAsync(
        string baseCode, IEnumerable<string> codes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseCode);
        ArgumentNullException.ThrowIfNull(codes);

        var quotes = await FetchAmdPerUnitAsync(ct);

        var upperBase = baseCode.ToUpperInvariant();
        var amdPerBase = AmdPerUnit(quotes, upperBase)
            ?? throw new InvalidOperationException(
                $"The Central Bank of Armenia does not quote '{upperBase}', so no rate can be expressed against it.");

        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var upper = code.ToUpperInvariant();
            var amdPerUnit = AmdPerUnit(quotes, upper);
            if (amdPerUnit is not null)
            {
                rates[upper] = amdPerUnit.Value / amdPerBase;
            }
        }

        return rates;
    }

    /// <summary>Downloads the bulletin and keeps only the properties that carry a usable decimal.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>"AMD per one unit", keyed by upper-case code; unquoted (null) codes are dropped.</returns>
    /// <exception cref="HttpRequestException">The bank did not answer with success.</exception>
    /// <exception cref="InvalidOperationException">The body was not the object the bank publishes.</exception>
    private async Task<Dictionary<string, decimal>> FetchAmdPerUnitAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(LatestRatesPath, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(ct)
            ?? throw new InvalidOperationException("The Central Bank of Armenia answered with an empty body.");

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

    /// <summary>Reads one quote, whichever of the shapes the bank has used for it.</summary>
    /// <remarks>
    /// The bulletin sends strings today; a number would be the more natural JSON and is accepted
    /// too, so a change on the bank's side does not turn every rate into "missing" overnight.
    /// </remarks>
    /// <param name="value">The property's value.</param>
    /// <returns>The positive decimal it holds, or <see langword="null"/> when there is none.</returns>
    private static decimal? ParseQuote(JsonElement value)
    {
        decimal parsed;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDecimal(out parsed):
                break;
            case JsonValueKind.String when decimal.TryParse(
                value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed):
                break;
            default:
                return null;
        }

        return parsed > 0 ? parsed : null;
    }

    /// <summary>How many AMD one unit of a currency is worth, the dram itself included.</summary>
    /// <param name="quotes">The parsed bulletin.</param>
    /// <param name="upperCode">An upper-case ISO 4217 alpha code.</param>
    /// <returns>The quote, or <see langword="null"/> when the bank does not carry it.</returns>
    private static decimal? AmdPerUnit(Dictionary<string, decimal> quotes, string upperCode)
    {
        if (upperCode == PivotCode)
        {
            return 1m;
        }

        return quotes.TryGetValue(upperCode, out var quote) ? quote : null;
    }
}
