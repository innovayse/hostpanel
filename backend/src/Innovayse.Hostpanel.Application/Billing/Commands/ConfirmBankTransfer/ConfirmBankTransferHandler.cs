using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ConfirmBankTransfer;

/// <summary>
/// Confirms that the manual bank transfer of an invoice arrived. The invoice is settled through the same domain path every
/// full payment takes (<c>Invoice.Settle</c>), so the payment history, the client ledger row and
/// <c>PaymentReceivedEvent</c> are written exactly as for any other payment, and the order the invoice was raised for is
/// fulfilled after the save, as a completed gateway payment does.
/// </summary>
/// <remarks>
/// A stored card charge of the invoice that has not finished refuses the confirm, as it refuses a payment recorded by hand;
/// a claim held for an operator does not, because settling it is what the operator is doing. Two confirms of one transfer
/// can never both pay: the second finds nothing waiting, or loses the concurrency check of the invoice and is answered the
/// same way.
/// </remarks>
public sealed class ConfirmBankTransferHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to fulfil the order the invoice was raised for.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the moment of the confirmation is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ConfirmBankTransferHandler> _logger;

    /// <summary>Puts the amount of a confirmed credit top-up on the client's balance in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to fulfil the order the invoice was raised for.</param>
    /// <param name="clock">The clock the moment of the confirmation is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clientCredit">Puts the amount of a confirmed credit top-up on the client's balance in the same save.</param>
    public ConfirmBankTransferHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activity,
        ILogger<ConfirmBankTransferHandler> logger,
        IClientCredit clientCredit)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _activity = activity;
        _logger = logger;
        _clientCredit = clientCredit;
    }

    /// <summary>Handles <see cref="ConfirmBankTransferCommand"/>.</summary>
    /// <param name="cmd">The confirm command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice; <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c> when no
    /// transfer waits on it (also the answer to a second confirm); <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> while a stored
    /// card charge of the invoice has not finished.
    /// </returns>
    public async Task<Result> HandleAsync(ConfirmBankTransferCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await LoadAsync(cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsBankTransferPending)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        }

        if (await _context.InvoicePaymentClaims.HasUnresolvedClaimAsync(invoice.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED);
        }

        var ledgerRow = invoice.ConfirmBankTransfer(_clock.UtcNow);
        if (ledgerRow is not null)
        {
            _context.Transactions.Add(ledgerRow);
        }

        // A confirmed top-up reaches the balance in the save that marks it paid.
        var credited = await _clientCredit.CreditPaidTopUpAsync(invoice, ledgerRow, ct);
        if (credited.IsFailure)
        {
            return credited;
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Confirmed Bank Transfer - Invoice ID: {invoice.Id}"), ct);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another writer changed the invoice between our read and our write: a second confirm, a reject, or the
            // client. The failed changes are dropped and the invoice is read as the database holds it now, so a lost
            // race is answered like the second confirm it is.
            _context.DetachAll();
            var current = await LoadAsync(cmd.InvoiceId, ct);
            if (current is null || current.IsBankTransferPending)
            {
                throw;
            }

            return Result.FailureCode(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        }

        // After the save, like a completed gateway payment: the order is fulfilled only once the money is on the books.
        // Fulfilment is idempotent, and a failure here leaves a paid invoice whose order staff can still accept by hand.
        var fulfilled = await _bus.InvokeAsync<Result>(new FulfillInvoicedOrderCommand(invoice.Id), ct);
        if (fulfilled.IsFailure)
        {
            _logger.LogWarning(
                "Bank transfer of invoice {InvoiceId} was confirmed but its order was not fulfilled: {ErrorCode}.",
                invoice.Id, fulfilled.ErrorCode);
        }

        return Result.Success();
    }

    /// <summary>Loads the invoice with the payments the settlement reads.</summary>
    /// <param name="invoiceId">The invoice id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked invoice, or <see langword="null"/> when the id names none.</returns>
    private Task<Invoice?> LoadAsync(int invoiceId, CancellationToken ct) =>
        _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
}
