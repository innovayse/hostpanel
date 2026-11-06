using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SelectMyBankTransfer;

/// <summary>
/// Moves one of the calling client's own invoices to <c>PaymentPending</c> as a manual bank transfer and answers with the
/// bank details and the reference to write on the transfer, refusing every invoice that is not theirs.
/// </summary>
/// <remarks>
/// The ownership check lives here rather than at the endpoint, so it travels with the message. The rules of a payment
/// start hold for a transfer too: a stored card charge that is in flight or held, a hosted payment still open at the bank
/// and an account that may not transact all refuse it, so the money cannot be taken twice. Choosing it again while it
/// already waits answers with the same details and changes nothing.
/// </remarks>
public sealed class SelectMyBankTransferHandler
{
    /// <summary>The rule that says a client may only pay their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, for the bank details and the account's status.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the moment of the choice is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only pay their own invoices.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, for the bank details and the account's status.</param>
    /// <param name="clock">The clock the moment of the choice is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public SelectMyBankTransferHandler(
        IInvoiceOwnership ownership,
        IHostpanelDbContext context,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activity)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
        _clock = clock;
        _activity = activity;
    }

    /// <summary>Handles <see cref="SelectMyBankTransferCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this pays the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The bank details and the reference, or <c>INVOICE_NOT_FOUND</c> (404) when the invoice is not the caller's;
    /// <c>BANK_TRANSFER_NOT_AVAILABLE</c> when the admin has it switched off; <c>INVOICE_NOT_PAYABLE</c> when the invoice
    /// is not open for payment; <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> or <c>INVOICE_STORED_CHARGE_HELD</c> while a stored
    /// card charge blocks it; <c>GATEWAY_SESSION_ALREADY_STARTED</c> while a hosted payment is still open; and the
    /// refusal of an account that is suspended or closed.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<BankTransferDetailsDto>> HandleAsync(SelectMyBankTransferCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<BankTransferDetailsDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var settings = await _bus.LoadBankTransferSettingsAsync(ct);

        // A transfer already waiting answers the same details again: a second click or a reload changes nothing.
        if (invoice.IsBankTransferPending)
        {
            return Result.Success(settings.ToDetails(invoice));
        }

        // A transfer with nothing to show the payer cannot be paid, so it is as unavailable as one switched off.
        if (!settings.IsEnabled || !settings.HasPayerDetails)
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.BANK_TRANSFER_NOT_AVAILABLE);
        }

        if (!invoice.IsPayable)
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        // The same claims that stop a hosted payment stop a transfer: a stored charge could take the money a second time.
        var blockedBy = await _context.InvoicePaymentClaims.BlockingClaimErrorAsync(invoice.Id, ct);
        if (blockedBy is not null)
        {
            return Result<BankTransferDetailsDto>.FailureCode(blockedBy);
        }

        var mayPay = await _bus.EnsureClientMayTransactAsync(invoice.ClientId, ct);
        if (mayPay.IsFailure)
        {
            return Result<BankTransferDetailsDto>.FailureCode(mayPay.ErrorCode!);
        }

        // A hosted payment the payer may still be completing at the bank would be lost once the invoice stops being payable.
        var now = _clock.UtcNow;
        if (invoice.IsInsideGatewaySessionWindow(now))
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.GATEWAY_SESSION_ALREADY_STARTED);
        }

        invoice.RequestBankTransfer(now);

        await _activity.RecordAsync(
            new ActivityLogEntry(invoice.ClientId, $"Chose Bank Transfer - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success(settings.ToDetails(invoice));
    }
}
