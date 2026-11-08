namespace Innovayse.Hostpanel.Application.Orders.Commands.RetryOrderLine;

/// <summary>
/// Command to run the fulfilment of one failed line of an accepted order again: the same registration,
/// transfer, renewal or service creation the first attempt ran. A line already fulfilled is refused, and a
/// domain the client already holds under the line's name is counted as done rather than registered twice.
/// </summary>
/// <param name="OrderId">The accepted order the line belongs to.</param>
/// <param name="ItemId">The failed line to retry.</param>
public sealed record RetryOrderLineCommand(int OrderId, int ItemId);
