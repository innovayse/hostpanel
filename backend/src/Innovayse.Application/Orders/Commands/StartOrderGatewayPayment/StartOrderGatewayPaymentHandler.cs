namespace Innovayse.Application.Orders.Commands.StartOrderGatewayPayment;

using Innovayse.Application.Orders.Extensions;
using Innovayse.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Domain.Orders.Interfaces;
using Wolverine;

/// <summary>
/// Resolves the invoice behind an order and starts a hosted-gateway payment on it.
/// </summary>
/// <remarks>
/// Dispatches <see cref="StartGatewayPaymentCommand"/> rather than repeating its work, the same
/// way <c>StartMyGatewayPaymentHandler</c> does for a client's own invoice: every check that
/// command makes -- payability, the allowed return-URL origins, the live-session window, the
/// currency match -- stays in one place, and this handler adds only the order-to-invoice step
/// the endpoint used to perform for itself.
/// </remarks>
/// <param name="orderRepo">Order repository, used to resolve the order's invoice.</param>
/// <param name="bus">Wolverine bus, used to reach the shared command once the invoice is known.</param>
public sealed class StartOrderGatewayPaymentHandler(IOrderRepository orderRepo, IMessageBus bus)
{
    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to pay, and how.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute gateway URL to redirect the payer's browser to.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no such order exists, when the payment token does not match it, and when the order
    /// was never billed; the shared command
    /// throws the same type for everything it refuses.
    /// </exception>
    public async Task<string> HandleAsync(StartOrderGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var order = (await orderRepo.FindByIdAsync(cmd.OrderId, ct))
            .EnsurePayableWith(cmd.OrderId, cmd.PaymentToken);

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {cmd.OrderId} has no linked invoice.");
        }

        return await bus.InvokeAsync<string>(
            new StartGatewayPaymentCommand(order.InvoiceId.Value, cmd.Module, cmd.ReturnUrl), ct);
    }
}
