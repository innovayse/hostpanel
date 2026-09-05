namespace Innovayse.Application.Orders.Commands.ConfirmOrderPayment;

using Innovayse.Application.Orders.Extensions;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Wolverine;

/// <summary>
/// Confirms a Stripe payment for an order: verifies the PaymentIntent succeeded,
/// marks the linked invoice as paid, and dispatches <see cref="FulfillPaidOrderCommand"/>
/// to accept the order and fulfill its line items.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="stripeService">Stripe payment service.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="bus">Wolverine message bus used to dispatch order fulfillment.</param>
public sealed class ConfirmOrderPaymentHandler(
    IOrderRepository orderRepo,
    IInvoiceRepository invoiceRepo,
    IStripeService stripeService,
    IUnitOfWork uow,
    IMessageBus bus)
{
    /// <summary>
    /// Handles <see cref="ConfirmOrderPaymentCommand"/>.
    /// </summary>
    /// <param name="cmd">The confirm order payment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the payment is confirmed and fulfillment is dispatched.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the order is not found or its payment token does not match, when it has no linked
    /// invoice, when the payment verification
    /// fails, or the gateway reports success without a transaction id.
    /// </exception>
    public async Task HandleAsync(ConfirmOrderPaymentCommand cmd, CancellationToken ct)
    {
        var order = (await orderRepo.FindByIdAsync(cmd.OrderId, ct))
            .EnsurePayableWith(cmd.OrderId, cmd.PaymentToken);

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {cmd.OrderId} has no linked invoice.");
        }

        var (success, transactionId) = await stripeService.VerifyPaymentIntentAsync(cmd.PaymentIntentId, ct);

        if (!success)
        {
            throw new InvalidOperationException(
                $"Payment verification failed for PaymentIntent {cmd.PaymentIntentId}.");
        }

        if (string.IsNullOrEmpty(transactionId))
        {
            throw new InvalidOperationException(
                $"Stripe reported success for PaymentIntent {cmd.PaymentIntentId} but returned no transaction id.");
        }

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        invoice.MarkPaid(transactionId);

        await uow.SaveChangesAsync(ct);

        await bus.InvokeAsync(new FulfillPaidOrderCommand(order.Id), ct);
    }
}
