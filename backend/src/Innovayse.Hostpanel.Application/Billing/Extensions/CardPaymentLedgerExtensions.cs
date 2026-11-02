using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Reads what the ledger already holds for a gateway transaction. A card or gateway payment settles
/// at most once, so every handler that records or hands back such money asks here first, and asks
/// the same question the same way: the committed rows, never the change tracker.
/// </summary>
public static class CardPaymentLedgerExtensions
{
    /// <summary>
    /// The invoices the ledger rows of a gateway transaction are linked to. A payment that settled an
    /// invoice is linked to it; money handed back (refunded, credited, held) is on the ledger under
    /// the same reference but linked to no invoice, which shows as a <see langword="null"/> entry.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="transactionId">The gateway's reference of the payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// One entry per ledger row of the transaction; empty when the transaction is not recorded. An
    /// empty or blank reference names no payment and is never recorded, so it answers empty without a query.
    /// </returns>
    public static async Task<IReadOnlyList<int?>> RecordedInvoiceIdsAsync(
        this IHostpanelDbContext context, string transactionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Last safe layer only: an empty reference names no payment, so it is answered as not recorded
        // without a query. That answer must never lead to a credit, so callers must not credit or
        // record money without a usable reference; they hold it for an operator instead.
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return [];
        }

        // Read as the database holds it: a row seen here was saved together with the Paid mark it belongs to.
        return await context.Transactions
            .TagWith(SettlementQueryTags.LedgerRead)
            .AsNoTracking()
            .Where(t => t.TransactionId == transactionId)
            .Select(t => t.InvoiceId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Whether the ledger already holds a row for a gateway payment, under the same key the unique
    /// index of gateway payments uses: the payment method and the reference together. The same
    /// reference under another method is another payment.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="paymentMethod">The payment method or gateway name the row is recorded under.</param>
    /// <param name="transactionId">The gateway's reference of the payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when at least one ledger row carries the method and the reference; a blank
    /// reference names no payment and answers <see langword="false"/> without a query.
    /// </returns>
    public static async Task<bool> IsRecordedAsync(
        this IHostpanelDbContext context, string paymentMethod, string transactionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return false;
        }

        return await context.Transactions
            .TagWith(SettlementQueryTags.LedgerRead)
            .AsNoTracking()
            .AnyAsync(t => t.PaymentMethod == paymentMethod && t.TransactionId == transactionId, ct);
    }
}
