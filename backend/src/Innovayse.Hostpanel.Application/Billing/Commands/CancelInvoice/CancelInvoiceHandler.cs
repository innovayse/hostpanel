using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;

/// <summary>
/// Cancels an invoice so it will not be collected. A hosted-gateway session it still holds is cancelled at the gateway when
/// the plugin can (<see cref="GatewayPaymentCanceller"/>). An invoice with a stored card charge that has not finished
/// is not cancelled: the charge may still take money, so the cancellation is refused until the charge has ended. A
/// claim held for an operator does not stop it, because cancelling is one way the operator settles it.
/// </summary>
public sealed class CancelInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Returns the invoice's credit to the client's balance in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Voids the gateway order of the invoice's unpaid hosted session.</summary>
    private readonly GatewayPaymentCanceller _canceller;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="clientCredit">Returns the invoice's credit to the client's balance in the same save.</param>
    /// <param name="canceller">Voids the gateway order of the invoice's unpaid hosted session.</param>
    public CancelInvoiceHandler(
        IHostpanelDbContext context, IActivityLogger activity, IClientCredit clientCredit, GatewayPaymentCanceller canceller)
    {
        _context = context;
        _activity = activity;
        _clientCredit = clientCredit;
        _canceller = canceller;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The cancel command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice, or
    /// <c>INVOICE_CANCEL_NOT_ALLOWED</c> when it is paid, refunded or already cancelled, or
    /// <c>INVOICE_HAS_PAYMENTS</c> when it already took a payment, or
    /// <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> when a stored card charge of it is in flight or waiting for the payer or the gateway, or
    /// <c>CLIENT_NOT_FOUND</c> when its credit has no client to go back to. Credit applied to the
    /// invoice goes back to the client's balance.
    /// </returns>
    public async Task<Result> HandleAsync(CancelInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Payments are loaded because an invoice that already took money may not be cancelled.
        var invoice = await _context.Invoices
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Refused before the credit moves, so a refusal leaves the balance untouched. A charge still in flight could
        // take money for an invoice that no longer exists to receive it, so the claim is waited out, not abandoned.
        if (await _context.InvoicePaymentClaims.HasUnresolvedClaimAsync(invoice.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        }

        invoice.EnsureCancellable();

        if (invoice.Credit > 0)
        {
            var returned = await _clientCredit.ReturnToBalanceAsync(invoice, invoice.Credit, ct);
            if (returned.IsFailure)
            {
                return returned;
            }
        }

        invoice.Cancel();
        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Cancelled Invoice - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        // A hosted session the payer could still complete is voided once the cancel is saved; a plugin that cannot is skipped.
        await _canceller.CancelSessionOfAsync(invoice, ct);
        return Result.Success();
    }
}
