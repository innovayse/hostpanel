using System.Diagnostics;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>
/// The <see cref="IExchangeRateSource"/> the application is wired to: it asks the bank the lookup names
/// (<see cref="ExchangeRateLookup.Provider"/>) and returns its answer unchanged, so no handler ever learns
/// which bank published the figures.
/// </summary>
/// <remarks>
/// The handler reads the operator's choice from the settings catalogue on every refresh, so a change in the admin
/// currencies page applies to the next refresh, the daily job included, without a restart. This class only picks the
/// adapter; it reads no setting.
/// </remarks>
public sealed class ConfiguredExchangeRateSource : IExchangeRateSource
{
    /// <summary>The Central Bank of Armenia's bulletin.</summary>
    private readonly CbaExchangeRateSource _cba;

    /// <summary>The European Central Bank's reference rates.</summary>
    private readonly EcbExchangeRateSource _ecb;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredExchangeRateSource"/> class.</summary>
    /// <param name="cba">The Central Bank of Armenia's bulletin.</param>
    /// <param name="ecb">The European Central Bank's reference rates.</param>
    public ConfiguredExchangeRateSource(CbaExchangeRateSource cba, EcbExchangeRateSource ecb)
    {
        _cba = cba;
        _ecb = ecb;
    }

    /// <inheritdoc/>
    public Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct)
    {
        IExchangeRateSource source = lookup.Provider switch
        {
            ExchangeRateProvider.Cba => _cba,
            ExchangeRateProvider.Ecb => _ecb,
            _ => throw new UnreachableException($"No exchange rate source is wired for {lookup.Provider}."),
        };

        return source.QuoteAsync(lookup, ct);
    }
}
