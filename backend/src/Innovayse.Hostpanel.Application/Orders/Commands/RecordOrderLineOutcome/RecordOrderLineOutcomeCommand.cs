using Innovayse.Hostpanel.Application.Orders.Common.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;

/// <summary>
/// Records how one attempt to fulfil an order line ended, on the line as the database holds it now, and lets the order's
/// status follow its lines. Sent by line fulfilment after each line, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The line the attempt was for.</param>
/// <param name="Outcome">Whether the attempt fulfilled the line or failed it.</param>
/// <param name="ErrorCode">The code a failed attempt ended with, or <see langword="null"/> for a fulfilled line or a refusal that gave none.</param>
public sealed record RecordOrderLineOutcomeCommand(int OrderId, int ItemId, OrderLineOutcome Outcome, string? ErrorCode = null);
