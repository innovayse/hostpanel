namespace Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;

/// <summary>
/// Claims the lines a run is about to do: each line that never ran or whose last attempt failed becomes in progress and its
/// attempt is counted, so a run that dies part way leaves its lines claimed (retryable once the claim runs out) and the
/// order Incomplete. Sent by line fulfilment before any work starts, never by a controller.
/// </summary>
/// <param name="OrderId">The order whose lines are claimed.</param>
/// <param name="ItemId">The one line to claim, or <see langword="null"/> to claim every line that is pending or failed.</param>
public sealed record ClaimOrderLinesCommand(int OrderId, int? ItemId = null);
