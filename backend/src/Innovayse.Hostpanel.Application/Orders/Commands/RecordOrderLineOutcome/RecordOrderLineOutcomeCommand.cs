using Innovayse.Hostpanel.Application.Orders.Common.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;

/// <summary>
/// Records how one attempt to fulfil an order line ended, on the line as the database holds it now, and lets the order's
/// status follow its lines. Sent by line fulfilment after each line, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The line the attempt was for.</param>
/// <param name="Attempt">
/// The attempt number the run was given when its claim on the line was saved. An outcome is recorded only while the line is
/// still in progress under that attempt, so a late outcome of an attempt whose claim ran out and was taken over by a newer
/// one is refused instead of overwriting the newer attempt's result.
/// </param>
/// <param name="Outcome">Whether the attempt fulfilled the line or failed it.</param>
/// <param name="ErrorCode">The code a failed attempt ended with, or <see langword="null"/> for a fulfilled line or a refusal that gave none.</param>
public sealed record RecordOrderLineOutcomeCommand(int OrderId, int ItemId, int Attempt, OrderLineOutcome Outcome, string? ErrorCode = null);
