using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.AddInvoicePayment;

/// <summary>
/// Records a manual payment, full or partial, against an invoice together with its client ledger
/// row. The invoice turns Paid once the recorded payments cover its total. A payment is refused while a stored card
/// charge of the invoice has not finished, so the same money cannot be recorded by hand and taken by the charge. A
/// claim held for an operator does not stop it: recording the payment is how the operator settles it.
/// </summary>
public sealed class AddInvoicePaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Puts the amount of a credit top-up invoice that this payment completes on the client's balance, in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="clientCredit">Puts the amount of a credit top-up invoice that this payment completes on the client's balance, in the same save.</param>
    public AddInvoicePaymentHandler(IHostpanelDbContext context, IActivityLogger activity, IClientCredit clientCredit)
    {
        _context = context;
        _activity = activity;
        _clientCredit = clientCredit;
    }

    /// <summary>Handles <see cref="AddInvoicePaymentCommand"/>.</summary>
    /// <param name="cmd">The add payment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice; an invoice that is not open
    /// for payment refuses with <c>INVOICE_NOT_PAYABLE</c> from the domain; <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> when a
    /// stored card charge of the invoice is in flight or waiting for the payer or the gateway; <c>CLIENT_NOT_FOUND</c> when the
    /// client of a credit top-up is gone.
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

        if (await _context.InvoicePaymentClaims.HasUnresolvedClaimAsync(invoice.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        }

        var payment = invoice.AddPayment(
            cmd.Date, cmd.Gateway, cmd.TransactionId, cmd.Amount, cmd.Fees, cmd.Notes, TransactionSource.Manual);
        _context.Transactions.Add(payment);

        // A top-up that this payment completes reaches the balance in the save that marks it paid.
        var credited = await _clientCredit.CreditPaidTopUpAsync(invoice, payment, ct);
        if (credited.IsFailure)
        {
            return credited;
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Added Invoice Payment - Invoice ID: {invoice.Id}"), ct);

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
