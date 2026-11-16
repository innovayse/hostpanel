namespace Innovayse.Hostpanel.Domain.Settings.Enums;

/// <summary>
/// The banks an installation can take its currency exchange rates from. The stored
/// <c>billing.exchange_rates.provider</c> value is the member name; the admin currencies page offers
/// the same list.
/// </summary>
public enum ExchangeRateProvider
{
    /// <summary>The Central Bank of Armenia's daily bulletin, quoted in drams; the default, so an existing installation keeps its source.</summary>
    Cba = 0,

    /// <summary>The European Central Bank's daily reference rates, quoted against the euro.</summary>
    Ecb = 1,
}
