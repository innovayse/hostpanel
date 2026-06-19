namespace Innovayse.Application.Orders.Commands.ConfirmOrderPayment;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Wolverine;

/// <summary>
/// Confirms a Stripe payment for an order: verifies the PaymentIntent succeeded,
/// marks the linked invoice as paid, accepts the order, and dispatches
/// <see cref="OrderServiceCommand"/> for each line item.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="stripeService">Stripe payment service.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="bus">Wolverine message bus for dispatching service creation commands.</param>
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
    /// <returns>A <see cref="Task"/> that completes when the payment is confirmed and services are dispatched.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the order is not found, has no linked invoice, or the payment verification fails.
    /// </exception>
    public async Task HandleAsync(ConfirmOrderPaymentCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

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

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        invoice.MarkPaid(transactionId!);
        order.Accept();

        await uow.SaveChangesAsync(ct);

        foreach (var item in order.Items)
        {
            await bus.InvokeAsync<int>(
                new OrderServiceCommand(order.ClientId, item.ProductId, item.BillingCycle,
                    item.FirstPaymentAmount, item.RecurringAmount, order.PaymentMethod), ct);
        }
    }
}
