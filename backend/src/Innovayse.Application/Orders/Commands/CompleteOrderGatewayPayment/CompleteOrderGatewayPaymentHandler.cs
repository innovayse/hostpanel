namespace Innovayse.Application.Orders.Commands.CompleteOrderGatewayPayment;

using Innovayse.Application.Orders.Extensions;
using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Domain.Orders.Interfaces;
using Wolverine;

/// <summary>
/// Resolves the invoice behind an order and verifies its hosted-gateway payment.
/// </summary>
/// <remarks>
/// Dispatches <see cref="CompleteGatewayPaymentCommand"/> rather than repeating it, so that
/// command's idempotency -- an already-paid invoice answers Paid without touching the gateway --
/// keeps holding for the checkout return leg as well as for the reconciliation cron.
/// </remarks>
/// <param name="orderRepo">Order repository, used to resolve the order's invoice.</param>
/// <param name="bus">Wolverine bus, used to reach the shared command once the invoice is known.</param>
public sealed class CompleteOrderGatewayPaymentHandler(IOrderRepository orderRepo, IMessageBus bus)
{
    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order whose payment should be verified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state the gateway reported for the session: paid, pending, or declined.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no such order exists, when its payment token does not match, and when the
    /// order was never billed.
    /// </exception>
    public async Task<GatewayCompletionState> HandleAsync(
        CompleteOrderGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var order = (await orderRepo.FindByIdAsync(cmd.OrderId, ct))
            .EnsurePayableWith(cmd.OrderId, cmd.PaymentToken);

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {cmd.OrderId} has no linked invoice.");
        }

        return await bus.InvokeAsync<GatewayCompletionState>(
            new CompleteGatewayPaymentCommand(order.InvoiceId.Value), ct);
    }
}
