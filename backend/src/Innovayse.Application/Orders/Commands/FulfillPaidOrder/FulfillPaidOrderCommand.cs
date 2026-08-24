namespace Innovayse.Application.Orders.Commands.FulfillPaidOrder;

/// <summary>
/// Fulfills an order whose invoice has just been paid: accepts it and dispatches
/// domain registration/transfer and service creation for each line item.
/// Safe to invoke more than once — a non-Pending order is a no-op.
/// </summary>
/// <param name="OrderId">The order primary key.</param>
public sealed record FulfillPaidOrderCommand(int OrderId);
