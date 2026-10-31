using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ConfirmOrderPayment;

/// <summary>
/// Confirms a card payment for an order: verifies the PaymentIntent succeeded and pays exactly
/// this order's invoice, marks the invoice as paid, and dispatches
/// <see cref="FulfillPaidOrderCommand"/> to accept the order and fulfill its line items.
/// </summary>
public sealed class ConfirmOrderPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Card payment gateway.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Wolverine message bus, for the invoice's brief, its settlement and order fulfillment.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">Card payment gateway.</param>
    /// <param name="bus">Wolverine message bus used to dispatch order fulfillment.</param>
    public ConfirmOrderPaymentHandler(IHostpanelDbContext context, ICardPaymentGateway cardGateway, IMessageBus bus)
    {
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and card gateway payment intent to confirm.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the payment is confirmed and fulfillment is dispatched, or
    /// <c>ORDER_NOT_FOUND</c> when no such order exists or the payment token does not match it,
    /// <c>ORDER_NOT_INVOICED</c> when the order was never billed, <c>INVOICE_NOT_FOUND</c> when
    /// its invoice has gone missing, <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card
    /// gateway, <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the card gateway
    /// cannot be reached, <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> when the card gateway cannot
    /// verify the PaymentIntent, <c>GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED</c> when it
    /// reports success without a transaction id, <c>PAYMENT_INTENT_MISMATCH</c> when the intent's
    /// order or invoice is not this order's, <c>PAYMENT_INTENT_ALREADY_USED</c> when the intent's
    /// payment is on the ledger for another payment, whatever the settlement refuses with once it has
    /// handed the money back (an amount or currency other than what is due among them), or whatever
    /// fulfillment itself refuses with. A retry after this intent already settled the invoice
    /// continues to fulfillment, which runs once.
    /// </returns>
    public async Task<Result> HandleAsync(ConfirmOrderPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var payable = await _context.FindPayableInvoicedOrderAsync(cmd.OrderId, cmd.PaymentToken, ct);
        if (payable.IsFailure)
        {
            return Result.FailureCode(payable.ErrorCode!);
        }

        var order = payable.Value!;
        var found = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(order.InvoiceId!.Value), ct);
        if (found.IsFailure)
        {
            return Result.FailureCode(found.ErrorCode!);
        }

        var invoice = found.Value!;

        // Without a card gateway key there is no intent to verify; refused before the gateway is asked.
        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return configured;
        }

        var intent = await _cardGateway.VerifyPaymentIntentAsync(new CardPaymentVerifyInstruction(cmd.PaymentIntentId), ct);
        if (intent.Failure == CardGatewayFailure.Unavailable)
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        if (!intent.Succeeded)
        {
            return Result.FailureCode(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        }

        if (string.IsNullOrEmpty(intent.TransactionId))
        {
            return Result.FailureCode(HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        }

        // The intent id comes from the caller, so a settled intent proves nothing until the
        // gateway's own record of it names this order and invoice. Its amount and currency are the
        // settlement's to compare with what is due, under the same read that records it.
        if (!PaysInvoice(intent, order.Id, invoice))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        }

        // Billing settles the invoice and writes its ledger row; a replay of this same intent
        // answers success, so a retry after a crash still reaches fulfillment below.
        var settled = await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(
                invoice.Id,
                intent.TransactionId,
                CurrencyCode.FromMinorUnits(intent.AmountMinor, intent.Currency),
                intent.Currency),
            ct);
        if (settled.IsFailure)
        {
            return settled;
        }

        return await _bus.InvokeAsync<Result>(new FulfillPaidOrderCommand(order.Id), ct);
    }

    /// <summary>Whether a settled intent was opened for this order and its invoice.</summary>
    /// <param name="intent">The intent as the card gateway records it.</param>
    /// <param name="orderId">The order being paid.</param>
    /// <param name="invoice">The order's invoice.</param>
    /// <returns><see langword="true"/> when the order and invoice both match.</returns>
    private static bool PaysInvoice(CardPaymentIntentStatus intent, int orderId, InvoiceBriefDto invoice) =>
        intent.OrderId == orderId && intent.InvoiceId == invoice.Id;
}
