using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one definition of income every income report reads, so the annual, forecast, revenue,
/// top-client and dashboard figures cannot disagree on what was earned and when.
/// </summary>
/// <remarks>
/// Income is the money an invoice really took: each payment row on the date it was taken, less
/// each refund row on the date it was given back. An invoice that turned Paid without a payment
/// row (credit covered it, or it is imported history) counts its <c>Total</c> at its
/// <c>PaidAt</c>; that total is already net of credit, so credit is never counted as income.
/// A partially refunded one counts the same way, less its refund rows. A refunded one keeps its <c>PaidAt</c>, so its total and its refund rows cancel out; a Refunded
/// invoice with no refund row (imported history) took and gave back nothing here.
/// Credit top-ups and credit refunds live only in the client ledger and are not invoice income.
/// Amounts stay in the invoice currency; a caller groups them by currency in SQL and converts
/// the few sums with <see cref="GetBaseCurrencyRatesAsync"/>.
/// </remarks>
public static class IncomeExtensions
{
    /// <summary>Months in a calendar year.</summary>
    private const int MonthsInYear = 12;

    /// <summary>Builds the income rows of every invoice, not yet filtered or materialized.</summary>
    /// <param name="context">The module's data context.</param>
    /// <returns>The query of income rows.</returns>
    public static IQueryable<IncomeEntry> IncomeEntries(this IHostpanelDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var moved = context.InvoiceTransactions
            .AsNoTracking()
            .Where(t => t.Type == InvoiceTransactionType.Payment || t.Type == InvoiceTransactionType.Refund)
            .Join(
                context.Invoices.AsNoTracking(),
                t => t.InvoiceId,
                i => i.Id,
                (t, i) => new IncomeEntry
                {
                    ClientId = i.ClientId,
                    Currency = i.Currency,
                    At = t.Date,
                    Amount = t.Type == InvoiceTransactionType.Payment ? t.Amount : -t.Amount,
                    Fees = t.Fees,
                });

        var settledWithoutPayment = context.Invoices
            .AsNoTracking()
            .Where(i => (Invoice.PaidStatuses.Contains(i.Status)
                    || (i.Status == InvoiceStatus.Refunded && i.Transactions.Any(t => t.Type == InvoiceTransactionType.Refund)))
                && i.PaidAt != null
                && !i.Transactions.Any(t => t.Type == InvoiceTransactionType.Payment))
            .Select(i => new IncomeEntry
            {
                ClientId = i.ClientId,
                Currency = i.Currency,
                At = i.PaidAt!.Value,
                Amount = i.Total,
                Fees = 0m,
            });

        return moved.Concat(settledWithoutPayment);
    }

    /// <summary>Keeps the income rows that moved inside a half-open window.</summary>
    /// <param name="entries">The income rows.</param>
    /// <param name="from">Start of the window (UTC), inclusive.</param>
    /// <param name="to">End of the window (UTC), exclusive.</param>
    /// <returns>The filtered query; the bounds compare the date columns themselves, so their indexes stay usable.</returns>
    public static IQueryable<IncomeEntry> Within(this IQueryable<IncomeEntry> entries, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Where(e => e.At >= from && e.At < to);
    }

    /// <summary>Reads the base currency and the rate of every currency to it.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The base currency with its conversion rates.</returns>
    public static async Task<BaseCurrencyRates> GetBaseCurrencyRatesAsync(this IHostpanelDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var baseCurrency = await context.GetBaseCurrencyAsync(ct);
        var rates = await context.GetRatesToBaseAsync(ct);
        return new BaseCurrencyRates(baseCurrency.Code, rates);
    }

    /// <summary>Sums the income of each month of a calendar year, converted to the base currency.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="year">The calendar year (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Twelve monthly amounts in the base currency.</returns>
    public static async Task<YearIncome> GetYearIncomeAsync(this IHostpanelDbContext context, int year, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddYears(1);

        // The database sums per currency and month; only those few rows come back to be converted.
        var sums = await context.IncomeEntries()
            .Within(from, to)
            .GroupBy(e => new { e.Currency, e.At.Month })
            .Select(g => new { g.Key.Currency, g.Key.Month, Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);
        var rates = await context.GetBaseCurrencyRatesAsync(ct);

        IReadOnlyList<decimal> months = [.. Enumerable.Range(1, MonthsInYear)
            .Select(month => sums.Where(s => s.Month == month).Sum(s => rates.ToBase(s.Amount, s.Currency)))];
        return new YearIncome(rates.Currency, months);
    }
}
