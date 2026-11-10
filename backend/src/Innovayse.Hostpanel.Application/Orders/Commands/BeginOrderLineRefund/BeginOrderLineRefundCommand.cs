namespace Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;

/// <summary>
/// Claims a failed order line for its refund before any money moves, so a retry cannot deliver the line while it is
/// being paid back. Sent by paid-order fulfilment, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The failed line whose cost is about to be given back.</param>
/// <param name="HeldAttempt">
/// The attempt that still holds the line because the run could not record its failure, or <see langword="null"/> for a
/// line whose failure is on record. The line is then claimed from in progress, since that run is the one refunding it.
/// </param>
/// <param name="ErrorCode">The code of the unrecorded failure, kept on the line with <paramref name="HeldAttempt"/>.</param>
public sealed record BeginOrderLineRefundCommand(int OrderId, int ItemId, int? HeldAttempt = null, string? ErrorCode = null);
