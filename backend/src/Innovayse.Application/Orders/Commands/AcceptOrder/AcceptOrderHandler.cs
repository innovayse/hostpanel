namespace Innovayse.Application.Orders.Commands.AcceptOrder;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Orders.Interfaces;
using Wolverine;

/// <summary>
/// Accepts a pending order, transitions it to Active, and dispatches
/// <see cref="OrderServiceCommand"/> for each line item to create client services.
/// Services are created in Pending status — no auto-provisioning occurs.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="bus">Wolverine message bus for dispatching service creation commands.</param>
public sealed class AcceptOrderHandler(
    IOrderRepository orderRepo,
    IUnitOfWork uow,
    IMessageBus bus)
{
    /// <summary>
    /// Handles <see cref="AcceptOrderCommand"/>.
    /// </summary>
    /// <param name="cmd">The accept order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the order is accepted and services are created.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the order is not found or not in Pending status.</exception>
    public async Task HandleAsync(AcceptOrderCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

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
