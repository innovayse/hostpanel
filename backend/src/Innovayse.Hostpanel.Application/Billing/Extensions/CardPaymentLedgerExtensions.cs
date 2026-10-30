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
    /// <returns>One entry per ledger row of the transaction; empty when the transaction is not recorded.</returns>
    public static async Task<IReadOnlyList<int?>> RecordedInvoiceIdsAsync(
        this IHostpanelDbContext context, string transactionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(transactionId);

        // Read as the database holds it: a row seen here was saved together with the Paid mark it belongs to.
        return await context.Transactions
            .TagWith("settle-ledger-read")
            .AsNoTracking()
            .Where(t => t.TransactionId == transactionId)
            .Select(t => t.InvoiceId)
            .ToListAsync(ct);
    }

    /// <summary>Whether the ledger already holds a row for a gateway transaction.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="transactionId">The gateway's reference of the payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when at least one ledger row carries the reference.</returns>
    public static async Task<bool> IsRecordedAsync(
        this IHostpanelDbContext context, string transactionId, CancellationToken ct)
    {
        return (await context.RecordedInvoiceIdsAsync(transactionId, ct)).Count > 0;
    }
}
