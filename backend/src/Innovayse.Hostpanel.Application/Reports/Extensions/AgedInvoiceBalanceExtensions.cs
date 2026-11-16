using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Extensions;

/// <summary>
/// The one definition of "what an open invoice still owes and how old it is", shared by the Aging
/// Invoices list and its summary so the two always agree on the amount and the period.
/// </summary>
public static class AgedInvoiceBalanceExtensions
{
    /// <summary>Most days past due an invoice in <see cref="AgingBucket.Days0To30"/> may be.</summary>
    private const int Days0To30Max = 30;

    /// <summary>Most days past due an invoice in <see cref="AgingBucket.Days30To60"/> may be.</summary>
    private const int Days30To60Max = 60;

    /// <summary>Most days past due an invoice in <see cref="AgingBucket.Days60To90"/> may be.</summary>
    private const int Days60To90Max = 90;

    /// <summary>Most days past due an invoice in <see cref="AgingBucket.Days90To120"/> may be.</summary>
    private const int Days90To120Max = 120;

    /// <summary>
    /// Projects the open (Unpaid or Overdue) invoices that still owe money to their balance and
    /// aging period, all in SQL.
    /// </summary>
    /// <param name="invoices">The invoices to read.</param>
    /// <param name="today">The UTC day the ages are measured on.</param>
    /// <returns>The query of balances, not yet materialized.</returns>
    /// <remarks>
    /// The amount owed is <see cref="Invoice.Total"/> (already net of applied credit) less the
    /// payments recorded on the invoice, the same figure as <see cref="Invoice.AmountDue"/>. Days
    /// past due count UTC calendar days, so an invoice due later today, or in the future, is in the
    /// first period. A period's lower edge is "due on or after midnight N days ago", which lets the
    /// database compare the stored instant directly.
    /// </remarks>
    public static IQueryable<AgedInvoiceBalance> SelectAgedBalances(this IQueryable<Invoice> invoices, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var within30 = today.AddDays(-Days0To30Max).StartOfDayUtc();
        var within60 = today.AddDays(-Days30To60Max).StartOfDayUtc();
        var within90 = today.AddDays(-Days60To90Max).StartOfDayUtc();
        var within120 = today.AddDays(-Days90To120Max).StartOfDayUtc();

        return invoices
            .Where(Invoice.PayableFilter)
            .Select(i => new AgedInvoiceBalance
            {
                InvoiceId = i.Id,
                ClientId = i.ClientId,
                Currency = i.Currency,
                DueDate = i.DueDate,
                AmountDue = i.Total - i.Transactions
                    .Where(t => t.Type == InvoiceTransactionType.Payment)
                    .Sum(t => t.Amount),
                Bucket = i.DueDate >= within30 ? AgingBucket.Days0To30
                    : i.DueDate >= within60 ? AgingBucket.Days30To60
                    : i.DueDate >= within90 ? AgingBucket.Days60To90
                    : i.DueDate >= within120 ? AgingBucket.Days90To120
                    : AgingBucket.Over120,
            })
            .Where(b => b.AmountDue > 0);
    }
}
