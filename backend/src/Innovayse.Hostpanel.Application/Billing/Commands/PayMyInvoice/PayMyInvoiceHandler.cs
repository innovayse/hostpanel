using System.Globalization;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.PayMyInvoice;

/// <summary>
/// Pays one of the calling client's own invoices with a payment method saved on their account,
/// marking it paid only after the card gateway reports the money taken.
/// </summary>
/// <remarks>
/// The ownership check lives here rather than at the endpoint, so it travels with the message:
/// nothing can charge a card through <see cref="PayMyInvoiceCommand"/> without it having run. The
/// charge goes through <see cref="ICardPaymentGateway"/>, the one port that can take money from a
/// saved method; there is no second, direct-charge abstraction. Once the money is taken the
/// invoice is settled by <see cref="SettleInvoiceCardPaymentCommand"/>, the one card settlement
/// every card payment goes through.
/// <para>
/// The charge carries an idempotency key built from the invoice and the payment attempt the client
/// chose for this pay action, so a retried request is one charge at the gateway, and the second
/// settlement is a replay of the first. A new pay action carries a new attempt, so a card the
/// gateway declined can be charged again at once instead of the decline being replayed.
/// </para>
/// </remarks>
public sealed class PayMyInvoiceHandler
{
    /// <summary>The rule that says a client may only pay their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Card payment gateway, which lists the saved methods and takes the money.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>The Wolverine bus, for the Clients module's card gateway customer query and the card settlement.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records the activity log line when an invoice already covered by its payments is settled.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of <see cref="PayMyInvoiceHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only pay their own invoices.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">Card payment gateway, which lists the saved methods and takes the money.</param>
    /// <param name="bus">The Wolverine bus, for the Clients module's card gateway customer query and the card settlement.</param>
    /// <param name="activity">Records the activity log line when an invoice already covered by its payments is settled.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public PayMyInvoiceHandler(
        IInvoiceOwnership ownership,
        IHostpanelDbContext context,
        ICardPaymentGateway cardGateway,
        IMessageBus bus,
        IActivityLogger activity,
        IClock clock)
    {
        _ownership = ownership;
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Handles <see cref="PayMyInvoiceCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this pays the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the gateway has taken the money and the invoice is marked paid, or at once
    /// without a charge when the payments it already holds cover it;
    /// <c>CLIENT_CARD_PAYMENT_DISABLED</c> (403) when the account has card processing disabled;
    /// <c>INVOICE_NOT_FOUND</c> (404) when the invoice is not the caller's, does not exist, or the
    /// caller has no client record; <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> (403) when the account
    /// may not pay; success without a second charge when the same attempt already
    /// settled it; <c>INVOICE_NOT_PAYABLE</c> when it is not open for payment otherwise;
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway;
    /// <c>NO_SAVED_PAYMENT_METHODS</c> when the account has never saved one;
    /// <c>PAYMENT_METHOD_NOT_OWNED</c> when the id is not saved on the caller's account;
    /// <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the gateway refuses to list or look up the
    /// account's payment methods or charges;
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the gateway cannot be reached (the same
    /// attempt may be retried, its idempotency key keeps it to one charge);
    /// <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> when the gateway declines the charge; or
    /// <c>GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED</c> when it reports success without a
    /// transaction id; or <c>PAYMENT_INTENT_ALREADY_USED</c> when the gateway's transaction is
    /// already on the ledger; or whatever <see cref="SettleInvoiceCardPaymentCommand"/> refuses with,
    /// once it has handed the money back. The invoice stays unpaid on every refusal.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(PayMyInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result.FailureCode(ownershipOutcome.ErrorCode!);
        }

        // Payments are loaded because only what is still due is charged.
        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // A suspended or closed account takes no new card payment; staff settle the account first.
        var mayPay = await _bus.EnsureClientMayTransactAsync(invoice.ClientId, ct);
        if (mayPay.IsFailure)
        {
            return mayPay;
        }

        // An account with card processing disabled has no saved card charged, whatever its status.
        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(invoice.ClientId, ct);
        if (mayPayByCard.IsFailure)
        {
            return mayPayByCard;
        }

        // Checked before any money moves: charging an invoice that can no longer be marked paid
        // would take the money and then fail to record it. A retry of the attempt that settled it
        // answers with that settlement instead.
        if (!invoice.IsPayable)
        {
            return await ReplaySettledAttemptAsync(invoice, cmd, ct);
        }

        // Payments recorded before the invoice was reopened (or a zero total) can already cover it;
        // charging the card for nothing would be refused by the gateway, so it settles on what it
        // holds, under the reference of the payment that covers it.
        if (invoice.AmountDue <= 0m)
        {
            var now = _clock.UtcNow;
            var covering = invoice.Transactions.LastOrDefault(t => t.Type == InvoiceTransactionType.Payment);
            invoice.Settle(
                covering?.Gateway ?? PaymentMethodNames.Card,
                covering?.TransactionId ?? $"no-charge-{invoice.Id}-{now.Ticks}",
                now, TransactionSource.Manual);
            await _activity.RecordAsync(
                new ActivityLogEntry(invoice.ClientId, $"Invoice Marked Paid - Invoice ID: {invoice.Id}"), ct);
            await _context.SaveChangesAsync(ct);
            return Result.Success();
        }

        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return configured;
        }

        // The ownership check above proved the invoice is the caller's, so the caller's own client
        // record is the invoice's.
        var customer = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (customer.IsFailure)
        {
            return Result.FailureCode(customer.ErrorCode!);
        }

        var customerId = customer.Value!.CustomerId;
        if (customerId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        }

        // The id came from the caller; only a method the gateway lists under this account's own
        // customer may be charged, so a foreign or invented id never reaches the charge.
        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(customerId), ct);
        if (methods.Failure is { } listFailure)
        {
            return Result.FailureCode(listFailure.ToPaymentMethodErrorCode());
        }

        if (!methods.Methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        // Charged in the currency the invoice was issued in.
        var amount = invoice.AmountDue;
        var (success, transactionId, failure) = await _cardGateway.ChargeSavedPaymentMethodAsync(
            new CardChargeInstruction(customerId, cmd.PaymentMethodId!, amount, invoice.Currency, invoice.Id, IdempotencyKey(invoice, cmd)), ct);

        if (failure == CardGatewayFailure.Unavailable)
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        if (!success)
        {
            return Result.FailureCode(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        }

        if (string.IsNullOrEmpty(transactionId))
        {
            return Result.FailureCode(HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        }

        return await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(invoice.Id, transactionId, amount, invoice.Currency), ct);
    }

    /// <summary>The idempotency key of the pay action: the invoice and the payment attempt the client chose for it.</summary>
    /// <param name="invoice">The invoice paid.</param>
    /// <param name="cmd">The command carrying the payment attempt.</param>
    /// <returns>The key the charge is sent and looked up with.</returns>
    private static string IdempotencyKey(Invoice invoice, PayMyInvoiceCommand cmd) =>
        string.Create(CultureInfo.InvariantCulture, $"invoice-{invoice.Id}-attempt-{cmd.AttemptId!.Value:N}");

    /// <summary>
    /// Answers a pay action on an invoice that is no longer open for payment. When this very attempt
    /// already took the money (a retry after a success whose answer was lost), the gateway's charge
    /// is found by its idempotency key, never charged again, and handed to the card settlement,
    /// which answers a replay of the payment that settled the invoice with success.
    /// </summary>
    /// <param name="invoice">The invoice, not payable.</param>
    /// <param name="cmd">The command carrying the payment attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The card settlement's answer for the attempt's charge; <c>INVOICE_NOT_PAYABLE</c> when the
    /// invoice is not settled or this attempt took no money; or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> / <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the
    /// gateway could not be asked, since then nothing is known about the attempt's money.
    /// </returns>
    private async Task<Result> ReplaySettledAttemptAsync(Invoice invoice, PayMyInvoiceCommand cmd, CancellationToken ct)
    {
        if (!invoice.IsSettled || !_cardGateway.IsConfigured)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        var customer = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        var customerId = customer.IsSuccess ? customer.Value!.CustomerId : null;
        if (customerId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        var charge = await _cardGateway.FindChargeAsync(
            new CardChargeLookupInstruction(customerId, invoice.Id, IdempotencyKey(invoice, cmd)), ct);
        if (charge.Failure is { } lookupFailure)
        {
            return Result.FailureCode(lookupFailure.ToPaymentMethodErrorCode());
        }

        if (!charge.Found || string.IsNullOrEmpty(charge.TransactionId))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        return await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(invoice.Id, charge.TransactionId, charge.Amount, charge.Currency), ct);
    }
}
