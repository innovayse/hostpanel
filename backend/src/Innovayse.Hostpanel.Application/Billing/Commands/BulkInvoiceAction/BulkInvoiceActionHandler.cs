using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BulkInvoiceAction;

/// <summary>Performs a bulk action on multiple invoices and returns the count of affected invoices.</summary>
public sealed class BulkInvoiceActionHandler
{
    /// <summary>Ledger payment method recorded for invoices an admin marks paid in bulk.</summary>
    private const string BulkPaymentMethod = PaymentMethodNames.Manual;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Returns cancelled invoices' credit to their clients' balances in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Returns cancelled invoices' credit to their clients' balances in the same save.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public BulkInvoiceActionHandler(IHostpanelDbContext context, IClientCredit clientCredit, IClock clock)
    {
        _context = context;
        _clientCredit = clientCredit;
        _clock = clock;
    }

    /// <summary>Handles <see cref="BulkInvoiceActionCommand"/>.</summary>
    /// <param name="cmd">The bulk action command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the number of invoices affected, <c>BULK_INVOICE_ACTION_UNKNOWN</c> when
    /// <paramref name="cmd"/> carries a value outside <see cref="InvoiceBulkAction"/>, or
    /// <c>CLIENT_NOT_FOUND</c> when a cancelled invoice's credit has no client to go back to; nothing
    /// is saved then.
    /// </returns>
    public async Task<Result<int>> HandleAsync(BulkInvoiceActionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!Enum.IsDefined(cmd.Action))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.BULK_INVOICE_ACTION_UNKNOWN);
        }

        var invoices = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .Where(i => cmd.InvoiceIds.Contains(i.Id))
            .ToListAsync(ct);

        var affected = cmd.Action switch
        {
            InvoiceBulkAction.MarkPaid => Result<int>.Success(MarkPaid(invoices)),
            InvoiceBulkAction.MarkUnpaid => Result<int>.Success(MarkUnpaid(invoices)),
            InvoiceBulkAction.MarkCancelled => await CancelAsync(invoices, ct),
            InvoiceBulkAction.Duplicate => Result<int>.Success(Duplicate(invoices)),
            InvoiceBulkAction.Delete => Result<int>.Success(await DeleteAsync(invoices, ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(cmd), cmd.Action, "Unhandled bulk invoice action."),
        };
        if (affected.IsFailure)
        {
            return affected;
        }

        await _context.SaveChangesAsync(ct);
        return affected;
    }

    /// <summary>Settles every payable invoice as paid manually, each with its ledger row.</summary>
    /// <param name="invoices">The selected invoices (tracked).</param>
    /// <returns>How many were settled.</returns>
    private int MarkPaid(IReadOnlyList<Invoice> invoices)
    {
        var now = _clock.UtcNow;
        var affected = 0;
        foreach (var invoice in invoices.Where(i => i.IsPayable))
        {
            var ledgerRow = invoice.Settle(
                BulkPaymentMethod, $"bulk-{invoice.Id}-{now.Ticks}", now, TransactionSource.Manual);
            if (ledgerRow is not null)
            {
                _context.Transactions.Add(ledgerRow);
            }

            affected++;
        }

        return affected;
    }

    /// <summary>
    /// Reverses every paid invoice that took no recorded payment back to Unpaid; one with payments
    /// is skipped, its money is undone by deleting or refunding the payment.
    /// </summary>
    /// <param name="invoices">The selected invoices (tracked).</param>
    /// <returns>How many were reversed.</returns>
    private static int MarkUnpaid(IReadOnlyList<Invoice> invoices)
    {
        var affected = 0;
        foreach (var invoice in invoices.Where(i => i.CanMarkUnpaid))
        {
            invoice.MarkUnpaid();
            affected++;
        }

        return affected;
    }

    /// <summary>Cancels every invoice that may still be cancelled, returning its applied credit to the client first.</summary>
    /// <param name="invoices">The selected invoices (tracked).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many were cancelled, or <c>CLIENT_NOT_FOUND</c> when a credit has no client to go back to.</returns>
    private async Task<Result<int>> CancelAsync(IReadOnlyList<Invoice> invoices, CancellationToken ct)
    {
        var cancellable = invoices.Where(i => i.CanCancel).ToList();
        foreach (var invoice in cancellable)
        {
            if (invoice.Credit > 0)
            {
                var returned = await _clientCredit.ReturnToBalanceAsync(invoice, invoice.Credit, ct);
                if (returned.IsFailure)
                {
                    return Result<int>.FailureCode(returned.ErrorCode!);
                }
            }

            invoice.Cancel();
        }

        return Result<int>.Success(cancellable.Count);
    }

    /// <summary>Adds a Draft copy of every invoice.</summary>
    /// <param name="invoices">The selected invoices.</param>
    /// <returns>How many copies were made.</returns>
    private int Duplicate(IReadOnlyList<Invoice> invoices)
    {
        var now = _clock.UtcNow;
        foreach (var invoice in invoices)
        {
            _context.Invoices.Add(invoice.Duplicate(now));
        }

        return invoices.Count;
    }

    /// <summary>Deletes every invoice that may be deleted, releasing the billable items that were put on them.</summary>
    /// <param name="invoices">The selected invoices (tracked).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    private async Task<int> DeleteAsync(IReadOnlyList<Invoice> invoices, CancellationToken ct)
    {
        var deletable = invoices.Where(i => i.CanDelete).ToList();
        await _context.ReleaseBillableItemsAsync(deletable.Select(i => i.Id).ToList(), ct);
        foreach (var invoice in deletable)
        {
            _context.Invoices.Remove(invoice);
        }

        return deletable.Count;
    }
}
