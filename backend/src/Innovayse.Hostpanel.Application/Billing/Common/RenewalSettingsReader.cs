using System.Globalization;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Reads <see cref="BillingSettingKeys"/> into <see cref="RenewalSettings"/>. A missing or
/// unparsable row falls back to its default with a warning: a typo in System Settings must not stop
/// billing, and handlers never parse setting strings themselves. The rows are read through
/// <see cref="BillingSettingSource"/>.
/// </summary>
public sealed class RenewalSettingsReader
{
    /// <summary>Reads the rows and falls back to the defaults.</summary>
    private readonly BillingSettingSource _source;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">Reads the rows and falls back to the defaults.</param>
    public RenewalSettingsReader(BillingSettingSource source)
    {
        _source = source;
    }

    /// <summary>Reads the five billing settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed settings.</returns>
    public async Task<RenewalSettings> ReadAsync(CancellationToken ct)
    {
        var keys = BillingSettingKeys.Defaults.Select(d => d.Key).ToList();
        var rows = await _source.ReadRowsAsync(keys, ct);

        return new RenewalSettings(
            Int(rows, BillingSettingKeys.InvoiceDaysBefore),
            Days(rows),
            Bool(rows, BillingSettingKeys.AutoApplyCredit),
            Int(rows, BillingSettingKeys.SuspendAfterOverdueDays),
            Int(rows, BillingSettingKeys.TerminateAfterSuspendedDays));
    }

    /// <summary>Reads an integer key.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <param name="key">The billing setting key, one of <c>BillingSettingKeys</c>.</param>
    /// <returns>The value.</returns>
    private int Int(IReadOnlyDictionary<string, string> rows, string key) =>
        int.Parse(_source.Value(rows, key), CultureInfo.InvariantCulture);

    /// <summary>Reads a boolean key.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <param name="key">The billing setting key, one of <c>BillingSettingKeys</c>.</param>
    /// <returns>The value.</returns>
    private bool Bool(IReadOnlyDictionary<string, string> rows, string key) =>
        bool.Parse(_source.Value(rows, key));

    /// <summary>Reads the reminder thresholds, distinct and largest first.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <returns>The thresholds.</returns>
    private IReadOnlyList<int> Days(IReadOnlyDictionary<string, string> rows) =>
        _source.Value(rows, BillingSettingKeys.ReminderDays)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(d => int.Parse(d, CultureInfo.InvariantCulture))
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
}
