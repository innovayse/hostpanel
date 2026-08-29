namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Maps ISO 4217 alpha currency codes — as stored on <see cref="Innovayse.Domain.Clients.Client.Currency"/> —
/// to their ISO 4217 numeric codes — as declared by hosted-gateway payment plugins via
/// <see cref="Innovayse.SDK.Plugins.IPaymentPlugin.CurrencyCode"/>. Extend this map before enabling
/// billing/payment in a new currency; an unmapped alpha code is treated as unsupported rather than
/// silently allowed through.
/// </summary>
public static class CurrencyCodes
{
    /// <summary>The alpha→numeric lookup table for currencies this panel plausibly bills in.</summary>
    private static readonly Dictionary<string, string> AlphaToNumeric = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "840",
        ["AMD"] = "051",
        ["EUR"] = "978",
        ["RUB"] = "643",
    };

    /// <summary>
    /// Number of minor units (cents/luma/etc.) per major currency unit for every currency this
    /// panel bills in — all are 2-decimal ISO 4217 currencies, so a single constant covers them.
    /// Used to convert a decimal amount into the integer minor-unit form gateway APIs expect.
    /// </summary>
    public const int MinorUnitsPerMajor = 100;

    /// <summary>
    /// Maps an ISO 4217 alpha currency code to its numeric equivalent.
    /// </summary>
    /// <param name="alpha">The alpha code (e.g. "USD", "AMD").</param>
    /// <returns>The numeric code (e.g. "840"), or <see langword="null"/> when the code is not mapped.</returns>
    public static string? ToNumeric(string alpha) =>
        AlphaToNumeric.TryGetValue(alpha, out var numeric) ? numeric : null;

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
