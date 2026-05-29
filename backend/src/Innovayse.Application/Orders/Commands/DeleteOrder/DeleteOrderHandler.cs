namespace Innovayse.Application.Orders.Commands.DeleteOrder;

using Innovayse.Application.Common;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;

/// <summary>
/// Permanently deletes an order. Only orders in Pending or Cancelled status may be deleted.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class DeleteOrderHandler(IOrderRepository orderRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteOrderCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the order is deleted.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the order is not found or is in Active or Fraud status.
    /// </exception>
    public async Task HandleAsync(DeleteOrderCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Cancelled))
        {
            throw new InvalidOperationException(
                $"Cannot delete an order with status {order.Status}. Only Pending or Cancelled orders can be deleted.");
        }

        orderRepo.Remove(order);
        await uow.SaveChangesAsync(ct);
    }
}
