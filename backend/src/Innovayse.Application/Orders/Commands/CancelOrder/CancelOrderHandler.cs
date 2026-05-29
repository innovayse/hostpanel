namespace Innovayse.Application.Orders.Commands.CancelOrder;

using Innovayse.Application.Common;
using Innovayse.Domain.Orders.Interfaces;

/// <summary>
/// Cancels a pending order, transitioning it to <see cref="Domain.Orders.OrderStatus.Cancelled"/>.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class CancelOrderHandler(IOrderRepository orderRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CancelOrderCommand"/>.
    /// </summary>
    /// <param name="cmd">The cancel order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the order is cancelled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the order is not found or not in Pending status.</exception>
    public async Task HandleAsync(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

        order.Cancel();
        await uow.SaveChangesAsync(ct);
    }
}
