namespace Innovayse.Application.Billing.Currencies.Commands.UpdateExchangeRates;

/// <summary>What an <see cref="UpdateExchangeRatesCommand"/> did.</summary>
/// <param name="Updated">Codes whose rate was rewritten from the source's quote.</param>
/// <param name="Missing">Codes the source did not quote; their stored rate was left alone.</param>
public record UpdateExchangeRatesResultDto(IReadOnlyList<string> Updated, IReadOnlyList<string> Missing);
