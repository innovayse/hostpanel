namespace Innovayse.Application.Billing.Currencies.Commands.UpdateExchangeRates;

/// <summary>Refreshes every non-base currency's rate from the configured exchange-rate source.</summary>
/// <remarks>
/// Rates alone. Prices are not recomputed here — that is <c>UpdateProductPricesCommand</c>, run
/// when the operator decides the new rates should reach the price list.
/// </remarks>
public record UpdateExchangeRatesCommand;
