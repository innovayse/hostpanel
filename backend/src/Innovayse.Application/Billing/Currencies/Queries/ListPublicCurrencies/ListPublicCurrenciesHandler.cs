namespace Innovayse.Application.Billing.Currencies.Queries.ListPublicCurrencies;

using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="ListPublicCurrenciesQuery"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
public sealed class ListPublicCurrenciesHandler(ICurrencyRepository currencies)
{
    /// <summary>Reads the enabled currencies, base first, without their rates.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per enabled currency.</returns>
    public async Task<IReadOnlyList<PublicCurrencyDto>> HandleAsync(ListPublicCurrenciesQuery query, CancellationToken ct)
    {
        var rows = await currencies.ListAsync(enabledOnly: true, ct);
        return rows
            .Select(c => new PublicCurrencyDto(c.Code, c.Numeric, c.Prefix, c.Suffix, c.Decimals, c.IsBase))
            .ToList();
    }
}
