namespace Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;

/// <summary>
/// Claims a failed order line for its refund before any money moves, so a retry cannot deliver the line while it is
/// being paid back. Sent by paid-order fulfilment, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The failed line whose cost is about to be given back.</param>
public sealed record BeginOrderLineRefundCommand(int OrderId, int ItemId);
