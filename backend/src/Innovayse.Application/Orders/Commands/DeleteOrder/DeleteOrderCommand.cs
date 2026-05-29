namespace Innovayse.Application.Orders.Commands.DeleteOrder;

/// <summary>Command to permanently delete an order that is Pending or Cancelled.</summary>
/// <param name="OrderId">The order to delete.</param>
public record DeleteOrderCommand(int OrderId);
