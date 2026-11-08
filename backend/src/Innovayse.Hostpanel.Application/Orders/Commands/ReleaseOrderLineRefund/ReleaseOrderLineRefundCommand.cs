using Innovayse.Hostpanel.Application.Orders.Common.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;

/// <summary>
/// Settles a refund that did not give back the whole cost of a failed line: a refund that returned nothing puts the line
/// back to failed so it can be retried, and one that returned only part leaves it for staff to finish by hand. Sent by
/// paid-order fulfilment, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The line that was being refunded.</param>
/// <param name="Outcome">How far the refund got.</param>
public sealed record ReleaseOrderLineRefundCommand(int OrderId, int ItemId, OrderLineRefundOutcome Outcome);
