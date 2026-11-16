using System.Diagnostics;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>
/// The <see cref="IExchangeRateSource"/> the application is wired to: it asks whichever bank the operator
/// chose in <see cref="BillingSettingKeys.RateProvider"/> and returns its answer unchanged, so no handler
/// ever learns which bank published the figures.
/// </summary>
/// <remarks>
/// The choice is read on every call, so a change in the admin currencies page applies to the next refresh,
/// the daily job included, without a restart. A missing or invalid row reads as the seeded default
/// (<see cref="ExchangeRateProvider.Cba"/>) with a warning, in <see cref="ExchangeRateSettingsReader"/>; this class
/// only picks the adapter.
/// </remarks>
public sealed class ConfiguredExchangeRateSource : IExchangeRateSource
{
    /// <summary>Reads the chosen bank from the settings catalogue.</summary>
    private readonly ExchangeRateSettingsReader _settings;

    /// <summary>The Central Bank of Armenia's bulletin.</summary>
    private readonly CbaExchangeRateSource _cba;

    /// <summary>The European Central Bank's reference rates.</summary>
    private readonly EcbExchangeRateSource _ecb;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredExchangeRateSource"/> class.</summary>
    /// <param name="settings">Reads the chosen bank from the settings catalogue.</param>
    /// <param name="cba">The Central Bank of Armenia's bulletin.</param>
    /// <param name="ecb">The European Central Bank's reference rates.</param>
    public ConfiguredExchangeRateSource(ExchangeRateSettingsReader settings, CbaExchangeRateSource cba, EcbExchangeRateSource ecb)
    {
        _settings = settings;
        _cba = cba;
        _ecb = ecb;
    }

    /// <inheritdoc/>
    public async Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct)
    {
        var provider = await _settings.ProviderAsync(ct);

        IExchangeRateSource source = provider switch
        {
            ExchangeRateProvider.Cba => _cba,
            ExchangeRateProvider.Ecb => _ecb,
            _ => throw new UnreachableException($"No exchange rate source is wired for {provider}."),
        };

        return await source.QuoteAsync(lookup, ct);
    }
}
