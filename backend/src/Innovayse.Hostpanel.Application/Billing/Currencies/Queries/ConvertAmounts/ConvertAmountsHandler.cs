using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ConvertAmounts;

/// <summary>
/// Handles <see cref="ConvertAmountsQuery"/> through <see cref="Domain.Billing.Currency.ConvertTo"/>,
/// the one conversion rule billing itself uses.
/// </summary>
public sealed class ConvertAmountsHandler
{
    /// <summary>Decimals an amount is shown to when it could not be converted.</summary>
    private const int UnconvertedDecimals = 2;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ConvertAmountsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The target currency and the amounts to convert.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The target currency and one converted amount per input amount, in the same order. With no
    /// target named, the base currency is the target. An amount whose own currency has no stored row
    /// is returned unchanged with two decimals and marked not converted, rather than guessed at. With no target named and no
    /// base currency stored, <c>BASE_CURRENCY_NOT_CONFIGURED</c>, since there is then no currency to
    /// answer in; with a target named that has no stored row, <c>CURRENCY_NOT_FOUND</c>, since
    /// unconverted amounts labelled with that code would be wrong prices.
    /// </returns>
    public async Task<Result<ConvertedAmountsDto>> HandleAsync(ConvertAmountsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Rates are read fresh on every call (a handful of rows) so an admin rate change shows at once.
        var currencies = await _context.Currencies
            .AsNoTracking()
            .ToListAsync(ct);

        var target = string.IsNullOrWhiteSpace(query.TargetCurrency)
            ? currencies.Find(c => c.IsBase)
            : currencies.Find(c => string.Equals(c.Code, query.TargetCurrency, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return Result<ConvertedAmountsDto>.FailureCode(string.IsNullOrWhiteSpace(query.TargetCurrency)
                ? HostpanelErrorCodes.BASE_CURRENCY_NOT_CONFIGURED
                : HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        }

        var converted = query.Amounts
            .Select(a =>
            {
                var source = currencies.Find(c => string.Equals(c.Code, a.Currency, StringComparison.OrdinalIgnoreCase));
                return source is not null
                    ? new ConvertedAmountDto(source.ConvertTo(a.Amount, target), target.Decimals, true)
                    : new ConvertedAmountDto(a.Amount, UnconvertedDecimals, false);
            })
            .ToList();

        return Result.Success(new ConvertedAmountsDto(target.Code, converted));
    }
}
