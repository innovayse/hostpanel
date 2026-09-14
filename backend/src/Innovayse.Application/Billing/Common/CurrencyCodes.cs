namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Turns a major-unit amount into the integer minor-unit form hosted-gateway payment plugins
/// expect.
/// </summary>
/// <remarks>
/// <see cref="ToMinorUnits"/> assumes the ISO 4217 exponent is 2 — a hundred minor units to the
/// major — for every currency it is handed. <see cref="Innovayse.Domain.Billing.Currency.Decimals"/>
/// is a display setting (how many places the storefront shows) and says nothing about the minor
/// unit, so it is deliberately not consulted here. A currency's numeric code lives on that same
/// row, which the operator configures, so no lookup table is compiled in.
/// </remarks>
public static class CurrencyCodes
{
    /// <summary>
    /// Number of minor units (cents/luma/etc.) per major currency unit for every currency this
    /// panel bills in — all are 2-decimal ISO 4217 currencies, so a single constant covers them.
    /// Used to convert a decimal amount into the integer minor-unit form gateway APIs expect.
    /// </summary>
    public const int MinorUnitsPerMajor = 100;

    /// <summary>
    /// Converts a decimal major-unit amount (e.g. 10.005 dollars) to its integer minor-unit
    /// form (e.g. 1001 cents), rounding half-away-from-zero so e.g. 10.0050 rounds to 1001
    /// rather than truncating to 1000.
    /// </summary>
    /// <param name="amount">The amount in major units.</param>
    /// <returns>The amount in minor units, as an integer.</returns>
    public static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * MinorUnitsPerMajor, MidpointRounding.AwayFromZero);
}
