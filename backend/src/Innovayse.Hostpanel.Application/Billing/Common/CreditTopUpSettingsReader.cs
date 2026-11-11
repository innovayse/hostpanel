using System.Globalization;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Reads the top-up limits (least, most, and how many may be open) of <see cref="BillingSettingKeys"/> into <see cref="CreditTopUpSettings"/>. A missing,
/// unparsable or contradictory pair (a minimum above the maximum) falls back to the seeded defaults with a warning: a typo
/// in System Settings must not let a client add funds without any limit. The rows are read through <see cref="BillingSettingSource"/>.
/// </summary>
public sealed class CreditTopUpSettingsReader
{
    /// <summary>Reads the rows and falls back to the defaults.</summary>
    private readonly BillingSettingSource _source;

    /// <summary>Warns about a contradictory pair.</summary>
    private readonly ILogger<CreditTopUpSettingsReader> _logger;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">Reads the rows and falls back to the defaults.</param>
    /// <param name="logger">Warns about a contradictory pair.</param>
    public CreditTopUpSettingsReader(BillingSettingSource source, ILogger<CreditTopUpSettingsReader> logger)
    {
        _source = source;
        _logger = logger;
    }

    /// <summary>Reads the top-up limits.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed limits, the defaults when the stored pair is unusable.</returns>
    public async Task<CreditTopUpSettings> ReadAsync(CancellationToken ct)
    {
        var keys = new List<string>
        {
            BillingSettingKeys.CreditTopUpMinimum, BillingSettingKeys.CreditTopUpMaximum, BillingSettingKeys.CreditTopUpMaxOpen,
        };
        var rows = await _source.ReadRowsAsync(keys, ct);

        var minimum = Parse(rows, BillingSettingKeys.CreditTopUpMinimum);
        var maximum = Parse(rows, BillingSettingKeys.CreditTopUpMaximum);
        var maxOpen = (int)Parse(rows, BillingSettingKeys.CreditTopUpMaxOpen);
        if (minimum <= maximum)
        {
            return new CreditTopUpSettings(minimum, maximum, maxOpen);
        }

        _logger.LogWarning("The top-up minimum {Minimum} is above the maximum {Maximum}; using the defaults.", minimum, maximum);
        return new CreditTopUpSettings(
            Default(BillingSettingKeys.CreditTopUpMinimum), Default(BillingSettingKeys.CreditTopUpMaximum), maxOpen);
    }

    /// <summary>The seeded default of <paramref name="key"/>.</summary>
    /// <param name="key">A top-up limit key.</param>
    /// <returns>Its default amount.</returns>
    private static decimal Default(string key) =>
        decimal.Parse(BillingSettingSource.Default(key), CultureInfo.InvariantCulture);

    /// <summary>Reads a stored amount, or the default with a warning when it is missing or does not match its pattern.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <param name="key">A top-up limit key.</param>
    /// <returns>A number above zero.</returns>
    private decimal Parse(IReadOnlyDictionary<string, string> rows, string key) =>
        decimal.Parse(_source.Value(rows, key), CultureInfo.InvariantCulture);
}
