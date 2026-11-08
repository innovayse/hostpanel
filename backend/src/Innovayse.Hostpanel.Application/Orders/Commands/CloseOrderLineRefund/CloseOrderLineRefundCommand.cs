namespace Innovayse.Hostpanel.Application.Orders.Commands.CloseOrderLineRefund;

/// <summary>
/// Closes an order line whose automatic refund returned only part of its cost, after staff gave back the rest by hand:
/// the line becomes Refunded for good and the order's status is brought in line with its lines. Sent by the order
/// page's close-refund action.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The partly refunded line.</param>
public sealed record CloseOrderLineRefundCommand(int OrderId, int ItemId);
