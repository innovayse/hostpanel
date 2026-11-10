namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderLineAction;

/// <summary>
/// Checks, right before one order line's action runs, that the run's claim still holds the line, and stamps the line so
/// the claim's lease counts from this moment. Sent by line fulfilment before each line, never by a controller.
/// </summary>
/// <param name="OrderId">The order the line belongs to.</param>
/// <param name="ItemId">The line about to run.</param>
/// <param name="Attempt">The attempt number the run was given when its claim on the line was saved.</param>
public sealed record StartOrderLineActionCommand(int OrderId, int ItemId, int Attempt);
