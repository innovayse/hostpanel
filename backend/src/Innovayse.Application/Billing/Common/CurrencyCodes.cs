namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Turns a major-unit amount into the integer minor-unit form hosted-gateway payment plugins
/// expect. The alpha-to-numeric lookup this class used to carry is gone: a currency's numeric code
/// now lives on the <see cref="Innovayse.Domain.Billing.Currency"/> row the operator configures,
/// so the panel is not limited to a list compiled into it.
/// </summary>
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
