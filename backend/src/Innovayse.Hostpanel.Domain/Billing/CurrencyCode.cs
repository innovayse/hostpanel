namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The one rule for the ISO 4217 alpha code an invoice, order, quote or transaction carries, so the
/// four aggregates cannot drift apart on what a valid code is.
/// </summary>
public static class CurrencyCode
{
    /// <summary>Length of an ISO 4217 alpha code, and the width of every column that stores one.</summary>
    public const int Length = 3;

    /// <summary>ISO 4217 exponent of every currency not listed in <see cref="ZeroDecimalCurrencies"/> or <see cref="ThreeDecimalCurrencies"/>.</summary>
    private const int DefaultExponent = 2;

    /// <summary>ISO 4217 exponent of the currencies in <see cref="ThreeDecimalCurrencies"/>.</summary>
    private const int ThreeDecimalExponent = 3;

    /// <summary>Currencies whose ISO 4217 minor unit is the major unit itself (exponent 0).</summary>
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG",
        "RWF", "UGX", "UYI", "VND", "VUV", "XAF", "XOF", "XPF",
    };

    /// <summary>Currencies whose ISO 4217 minor unit is a thousandth of the major unit (exponent 3).</summary>
    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND",
    };

    /// <summary>
    /// Returns the ISO 4217 exponent (number of minor-unit decimal places) of a currency. This is the
    /// money precision; <see cref="Currency.Decimals"/> is only how many places the storefront shows.
    /// </summary>
    /// <param name="code">The ISO 4217 code, any case.</param>
    /// <returns>0, 2 or 3.</returns>
    public static int MinorUnitExponent(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (ZeroDecimalCurrencies.Contains(code))
        {
            return 0;
        }

        return ThreeDecimalCurrencies.Contains(code) ? ThreeDecimalExponent : DefaultExponent;
    }

    /// <summary>
    /// Rounds a major-unit amount to the currency's minor unit, half away from zero (10.005 USD is
    /// 10.01, 1500.5 JPY is 1501, 1.0005 KWD is 1.001), so it is an amount a payer can really be
    /// charged or refunded.
    /// </summary>
    /// <param name="amount">The amount in major units.</param>
    /// <param name="code">The ISO 4217 code of the amount's currency, any case.</param>
    /// <returns>The amount rounded to the currency's minor unit, still in major units.</returns>
    public static decimal RoundToMinorUnits(decimal amount, string code) =>
        Math.Round(amount, MinorUnitExponent(code), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts a decimal major-unit amount to its integer minor-unit form in the given currency,
    /// the form every payment gateway expects, rounding half away from zero (10.005 USD is 1001
    /// cents, 1500.5 JPY is 1501 yen, 1.0005 KWD is 1001 fils) and never truncating.
    /// </summary>
    /// <param name="amount">The amount in major units.</param>
    /// <param name="code">The ISO 4217 code of the amount's currency, any case.</param>
    /// <returns>The amount in minor units, as an integer.</returns>
    public static long ToMinorUnits(decimal amount, string code)
    {
        // The same rounding the domain applies to money, so a gateway amount and an invoice amount agree.
        return (long)(RoundToMinorUnits(amount, code) * MinorUnitScale(code));
    }

    /// <summary>
    /// Converts an integer minor-unit amount back to its decimal major-unit form in the given
    /// currency (1001 cents is 10.01 USD, 1501 yen is 1501 JPY, 1001 fils is 1.001 KWD).
    /// </summary>
    /// <param name="amountMinor">The amount in minor units.</param>
    /// <param name="code">The ISO 4217 code of the amount's currency, any case.</param>
    /// <returns>The amount in major units.</returns>
    public static decimal FromMinorUnits(long amountMinor, string code)
    {
        return amountMinor / MinorUnitScale(code);
    }

    /// <summary>How many minor units make one major unit of the currency (100 for USD, 1 for JPY, 1000 for KWD).</summary>
    /// <param name="code">The ISO 4217 code, any case.</param>
    /// <returns>Ten to the power of the currency's ISO 4217 exponent.</returns>
    public static decimal MinorUnitScale(string code)
    {
        var scale = 1m;
        for (var i = 0; i < MinorUnitExponent(code); i++)
        {
            scale *= 10m;
        }

        return scale;
    }

    /// <summary>
    /// Whether a value is exactly an ISO 4217 alpha code as stored: three ASCII letters, with no
    /// padding. Digits, punctuation, whitespace and non-ASCII letters all fail. Case is not part of
    /// the shape; <see cref="Normalise"/> upper-cases.
    /// </summary>
    /// <param name="currency">The value to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is three ASCII letters.</returns>
    public static bool IsValid(string? currency) =>
        currency is { Length: Length } && currency.All(char.IsAsciiLetter);

    /// <summary>
    /// Trims, validates and upper-cases a currency code; the non-throwing form of
    /// <see cref="Normalise"/> for callers that skip bad data rather than fail on it.
    /// </summary>
    /// <param name="currency">The raw code, or <see langword="null"/>.</param>
    /// <param name="normalised">The trimmed, upper-cased code when the result is <see langword="true"/>; otherwise empty.</param>
    /// <returns><see langword="true"/> when the trimmed value is three ASCII letters.</returns>
    public static bool TryNormalise(string? currency, out string normalised)
    {
        var trimmed = currency?.Trim();
        if (!IsValid(trimmed))
        {
            normalised = string.Empty;
            return false;
        }

        normalised = trimmed!.ToUpperInvariant();
        return true;
    }

    /// <summary>Trims, validates and upper-cases a currency code.</summary>
    /// <param name="currency">The raw code.</param>
    /// <returns>The code in upper case.</returns>
    /// <exception cref="ArgumentException">The code, once trimmed, is not exactly three ASCII letters.</exception>
    public static string Normalise(string currency)
    {
        if (!TryNormalise(currency, out var normalised))
        {
            throw new ArgumentException("A three-letter ISO 4217 currency is required.", nameof(currency));
        }

        return normalised;
    }
}
