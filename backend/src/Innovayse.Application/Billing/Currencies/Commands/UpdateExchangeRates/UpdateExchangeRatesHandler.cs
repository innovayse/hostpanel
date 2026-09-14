namespace Innovayse.Application.Billing.Currencies.Commands.UpdateExchangeRates;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="UpdateExchangeRatesCommand"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
/// <param name="source">Where the quotes come from.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class UpdateExchangeRatesHandler(
    ICurrencyRepository currencies,
    IExchangeRateSource source,
    IUnitOfWork uow)
{
    /// <summary>Asks the source for every non-base code and writes what it quotes.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Which codes were updated and which the source lacked.</returns>
    public async Task<UpdateExchangeRatesResultDto> HandleAsync(UpdateExchangeRatesCommand cmd, CancellationToken ct)
    {
        var all = await currencies.ListAsync(enabledOnly: false, ct);
        var baseCurrency = all.Single(c => c.IsBase);
        var others = all.Where(c => !c.IsBase).ToList();

        var rates = await source.RatesToAsync(baseCurrency.Code, others.Select(c => c.Code), ct);

        List<string> updated = [];
        List<string> missing = [];

        foreach (var currency in others)
        {
            if (rates.TryGetValue(currency.Code, out var rate))
            {
                currency.UpdateRate(rate);
                updated.Add(currency.Code);
            }
            else
            {
                missing.Add(currency.Code);
            }
        }

        await uow.SaveChangesAsync(ct);
        return new UpdateExchangeRatesResultDto(updated, missing);
    }
}
