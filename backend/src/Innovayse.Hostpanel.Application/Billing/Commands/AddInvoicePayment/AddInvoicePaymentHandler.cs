using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.AddInvoicePayment;

/// <summary>
/// Records a manual payment, full or partial, against an invoice together with its client ledger
/// row. The invoice turns Paid once the recorded payments cover its total.
/// </summary>
public sealed class AddInvoicePaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public AddInvoicePaymentHandler(IHostpanelDbContext context, IActivityLogger activity)
    {
        _context = context;
        _activity = activity;
    }

    /// <summary>Handles <see cref="AddInvoicePaymentCommand"/>.</summary>
    /// <param name="cmd">The add payment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice; an invoice that is not open
    /// for payment refuses with <c>INVOICE_NOT_PAYABLE</c> from the domain.
    /// </returns>
    public async Task<Result> HandleAsync(AddInvoicePaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        _context.Transactions.Add(
            invoice.AddPayment(
                cmd.Date, cmd.Gateway, cmd.TransactionId, cmd.Amount, cmd.Fees, cmd.Notes, TransactionSource.Manual));

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Added Invoice Payment - Invoice ID: {invoice.Id}"), ct);

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
