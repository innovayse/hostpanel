namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineRefunded;

/// <summary>
/// Records that the cost of a failed order line was given back to the client, which closes the line: no
/// retry can then deliver what was refunded. Sent by paid-order fulfilment, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The refunded line.</param>
public sealed record RecordOrderLineRefundedCommand(int OrderId, int ItemId);
