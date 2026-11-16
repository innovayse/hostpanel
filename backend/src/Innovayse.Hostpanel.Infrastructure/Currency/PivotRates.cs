using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>
/// The arithmetic every rate source shares. A bank publishes its figures against one currency of its
/// own, the <i>pivot</i> (the dram for the Central Bank of Armenia, the euro for the European Central
/// Bank); the panel asks for "one unit of a currency in the base", whatever the base is. This turns the
/// first into the second, so no adapter repeats it.
/// </summary>
public static class PivotRates
{
    /// <summary>Reads a positive decimal written with an invariant culture, as both banks publish it.</summary>
    /// <param name="text">The published text, or <see langword="null"/> when there is none.</param>
    /// <returns>The value, or <see langword="null"/> when the text is missing, not a number or not above zero.</returns>
    public static decimal? ParsePositive(string? text)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    /// <summary>Converts "pivot per one unit" quotes into "one unit in the base" quotes for the requested codes.</summary>
    /// <remarks>
    /// With <c>p(x)</c> the pivot value of one unit of <c>x</c> and <c>p(pivot) = 1</c>, one unit of
    /// <c>code</c> in <c>base</c> is <c>p(code) / p(base)</c>. A base the bank does not quote cannot anchor
    /// any of this, so the call refuses rather than guess; a requested code the bank does not quote, or quotes at nothing above zero, is
    /// left out of the answer.
    /// </remarks>
    /// <param name="lookup">The base currency and the codes to quote.</param>
    /// <param name="pivotCode">The currency the bank quotes everything in; it is worth 1 of itself and is not in <paramref name="pivotPerUnit"/>.</param>
    /// <param name="pivotPerUnit">What one unit of each currency is worth in the pivot, keyed by upper-case code.</param>
    /// <returns>
    /// The quotes; <c>EXCHANGE_RATE_BASE_NOT_QUOTED</c> when the base is not among them; or
    /// <c>EXCHANGE_RATE_SOURCE_UNAVAILABLE</c> when the base is quoted at nothing above zero, a feed no division can use.
    /// </returns>
    public static Result<ExchangeRateQuotes> Quote(
        ExchangeRateLookup lookup,
        string pivotCode,
        IReadOnlyDictionary<string, decimal> pivotPerUnit)
    {
        var pivotPerBase = PivotPerUnit(pivotCode, pivotPerUnit, lookup.BaseCode.ToUpperInvariant());
        if (pivotPerBase is null)
        {
            return Result<ExchangeRateQuotes>.FailureCode(HostpanelErrorCodes.EXCHANGE_RATE_BASE_NOT_QUOTED);
        }

        // A quote that came out as zero (a bank figure so large its inverse rounds away) cannot be a divisor:
        // the feed is unusable, and is reported as the source being unavailable like any other bad answer.
        if (pivotPerBase <= 0)
        {
            return Result<ExchangeRateQuotes>.FailureCode(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
        }

        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var code in lookup.Codes)
        {
            var upper = code.ToUpperInvariant();
            var pivotPerCode = PivotPerUnit(pivotCode, pivotPerUnit, upper);
            if (pivotPerCode > 0)
            {
                rates[upper] = pivotPerCode.Value / pivotPerBase.Value;
            }
        }

        return Result<ExchangeRateQuotes>.Success(new ExchangeRateQuotes(rates));
    }

    /// <summary>What one unit of a currency is worth in the pivot, the pivot itself included.</summary>
    /// <param name="pivotCode">The pivot currency.</param>
    /// <param name="pivotPerUnit">The parsed quotes.</param>
    /// <param name="upperCode">An upper-case ISO 4217 alpha code.</param>
    /// <returns>The value, or <see langword="null"/> when the bank does not carry the currency.</returns>
    private static decimal? PivotPerUnit(string pivotCode, IReadOnlyDictionary<string, decimal> pivotPerUnit, string upperCode)
    {
        if (upperCode == pivotCode)
        {
            return 1m;
        }

        return pivotPerUnit.TryGetValue(upperCode, out var quote) ? quote : null;
    }
}
