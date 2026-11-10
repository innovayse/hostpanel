using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Where exchange rates come from when the operator asks for them to be refreshed.</summary>
/// <remarks>
/// The contract is in the panel's own terms ("one unit of a currency is worth this much of the
/// base"), so a handler never learns which bank published the figures or which currency that
/// bank pivots on. Implementations live in Infrastructure.
/// </remarks>
public interface IExchangeRateSource
{
    /// <summary>Quotes each requested currency against the base.</summary>
    /// <param name="lookup">The base currency and the codes to quote.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The quotes, or <c>EXCHANGE_RATE_SOURCE_UNAVAILABLE</c> when the source did not answer, answered
    /// with an error or with a body it does not publish, or <c>EXCHANGE_RATE_BASE_NOT_QUOTED</c> when
    /// the source cannot express anything against the base currency.
    /// </returns>
    public Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct);
}
