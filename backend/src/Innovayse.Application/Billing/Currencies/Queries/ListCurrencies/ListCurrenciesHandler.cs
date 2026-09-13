namespace Innovayse.Application.Billing.Currencies.Queries.ListCurrencies;

using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="ListCurrenciesQuery"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
public sealed class ListCurrenciesHandler(ICurrencyRepository currencies)
{
    /// <summary>Reads the currencies, base first, then by code.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per currency.</returns>
    public async Task<IReadOnlyList<CurrencyDto>> HandleAsync(ListCurrenciesQuery query, CancellationToken ct)
    {
        var rows = await currencies.ListAsync(query.EnabledOnly, ct);
        return rows
            .Select(c => new CurrencyDto(c.Code, c.Numeric, c.Prefix, c.Suffix, c.Decimals, c.RateToBase, c.IsBase, c.IsEnabled))
            .ToList();
    }
}
