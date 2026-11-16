using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Common;

/// <summary>
/// Reads which bank the exchange rates come from, the <see cref="BillingSettingKeys.RateProvider"/> row. A missing or
/// invalid row falls back to the seeded default (<see cref="ExchangeRateProvider.Cba"/>) with a warning, so a typo in
/// System Settings never stops a refresh. The row is read through <see cref="SettingSource"/>.
/// </summary>
public sealed class ExchangeRateSettingsReader
{
    /// <summary>Reads the row and falls back to the default.</summary>
    private readonly SettingSource _source;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">Reads the row and falls back to the default.</param>
    public ExchangeRateSettingsReader(SettingSource source)
    {
        _source = source;
    }

    /// <summary>Reads the bank the next refresh asks.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chosen bank; <see cref="ExchangeRateProvider.Cba"/> when the row is missing or invalid.</returns>
    public async Task<ExchangeRateProvider> ProviderAsync(CancellationToken ct)
    {
        var rows = await _source.ReadRowsAsync([BillingSettingKeys.RateProvider], ct);

        // The key's pattern accepts a member name in any case and the stored row is kept as written, so the
        // parse ignores case; the value is a match of that pattern, or the seeded default, so it always parses.
        return Enum.Parse<ExchangeRateProvider>(_source.Value(rows, BillingSettingKeys.RateProvider), ignoreCase: true);
    }
}
