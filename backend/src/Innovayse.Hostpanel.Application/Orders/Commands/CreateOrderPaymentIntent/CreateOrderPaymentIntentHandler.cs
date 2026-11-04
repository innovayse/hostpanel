using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CreateOrderPaymentIntent;

/// <summary>
/// Handles <see cref="CreateOrderPaymentIntentCommand"/>: resolves the order's invoice, and
/// registers an intent to charge what is still due on it at the card gateway.
/// </summary>
public sealed class CreateOrderPaymentIntentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The card gateway this backend opens intents at.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>The message bus, for the invoice's brief; Orders never reads the Billing tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">The card gateway this backend opens intents at.</param>
    /// <param name="bus">The message bus, for the invoice's brief.</param>
    public CreateOrderPaymentIntentHandler(IHostpanelDbContext context, ICardPaymentGateway cardGateway, IMessageBus bus)
    {
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to open a payment intent for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The intent's client secret, or <c>ORDER_NOT_FOUND</c> when no such order exists or the
    /// payment token does not match it, <c>ORDER_NOT_INVOICED</c> when the order was never
    /// billed, <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the order's account may not pay, <c>CLIENT_CARD_PAYMENT_DISABLED</c> when it has card processing disabled, <c>INVOICE_NOT_FOUND</c> when its invoice has gone missing, or
    /// <c>INVOICE_NOT_PAYABLE</c> when the invoice is not open or owes nothing,
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway,
    /// <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> when the card gateway refuses the intent, or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the card gateway cannot be reached.
    /// </returns>
    public async Task<Result<PaymentIntentDto>> HandleAsync(CreateOrderPaymentIntentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var payable = await _context.FindPayableInvoicedOrderAsync(cmd.OrderId, cmd.PaymentToken, ct);
        if (payable.IsFailure)
        {
            return Result<PaymentIntentDto>.FailureCode(payable.ErrorCode!);
        }

        var order = payable.Value!;

        // A suspended or closed account opens no new payment, whatever order it was placed on.
        var mayPay = await _bus.EnsureClientMayTransactAsync(order.ClientId, ct);
        if (mayPay.IsFailure)
        {
            return Result<PaymentIntentDto>.FailureCode(mayPay.ErrorCode!);
        }

        // An account with card processing disabled opens no card payment, whatever its status.
        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(order.ClientId, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<PaymentIntentDto>.FailureCode(mayPayByCard.ErrorCode!);
        }

        var found = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(order.InvoiceId!.Value), ct);
        if (found.IsFailure)
        {
            return Result<PaymentIntentDto>.FailureCode(found.ErrorCode!);
        }

        var invoice = found.Value!;

        // A paid, cancelled or nothing-owed invoice has nothing to charge, and the gateway refuses
        // an intent for zero; refused here with a code rather than as the gateway's failure.
        if (invoice.Status is not (InvoiceStatus.Unpaid or InvoiceStatus.Overdue) || invoice.AmountDue <= 0)
        {
            return Result<PaymentIntentDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        // Checked after the order and invoice are known to be payable and before the gateway is asked,
        // so a deployment without a card gateway key answers with a code instead of failing on its empty key.
        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return Result<PaymentIntentDto>.FailureCode(configured.ErrorCode!);
        }

        // The ids travel with the intent so the gateway's own dashboard and reconciliation name the same rows
        // this database does; a payment that arrives with no way back to an order is a manual reconciliation.
        // The intent is opened in the invoice's own currency for what is still due, the amount the
        // settlement records; payments already on the invoice are not charged again.
        var intent = await _cardGateway.CreatePaymentIntentAsync(
            new CardPaymentIntentInstruction(invoice.AmountDue, invoice.Currency, order.Id, invoice.Id, order.ClientId, IdempotencyKey(invoice)), ct);
        return intent.Failure switch
        {
            null => Result.Success(new PaymentIntentDto(intent.ClientSecret!)),
            CardGatewayFailure.Unavailable => Result<PaymentIntentDto>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE),
            CardGatewayFailure.NotConfigured => Result<PaymentIntentDto>.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE),
            _ => Result<PaymentIntentDto>.FailureCode(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED),
        };
    }

    /// <summary>
    /// The idempotency key of the intent: the invoice and the amount still due on it, so a repeated
    /// request for the same attempt gets the same intent back, while a changed amount (a part
    /// payment landed in between) opens a new one.
    /// </summary>
    /// <param name="invoice">The invoice the intent is opened for.</param>
    /// <returns>The key the intent is opened with.</returns>
    private static string IdempotencyKey(InvoiceBriefDto invoice) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"invoice-{invoice.Id}-intent-{CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency)}-{invoice.Currency.ToUpperInvariant()}");
}
