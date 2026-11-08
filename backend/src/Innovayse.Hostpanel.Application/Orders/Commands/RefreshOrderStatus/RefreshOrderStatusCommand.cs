namespace Innovayse.Hostpanel.Application.Orders.Commands.RefreshOrderStatus;

/// <summary>
/// Makes an accepted order's status say what its lines say now. Lines of one order finish at the same moment, each
/// run recording its own line while looking at the others as they were when it began, so after the lines are saved the
/// status is settled from the lines as they are in the database. Sent by line fulfilment, never by a controller.
/// </summary>
/// <param name="OrderId">The order whose status is brought in line with its lines.</param>
public sealed record RefreshOrderStatusCommand(int OrderId);
