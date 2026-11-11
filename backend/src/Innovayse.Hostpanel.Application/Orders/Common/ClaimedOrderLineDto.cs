namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>
/// One order line a claim gave to a run, with the attempt number it was given. The run starts, records and releases the
/// line under exactly this attempt, so a line another claim holds, or one a newer attempt took over, is never run by it.
/// </summary>
/// <param name="ItemId">The claimed order line.</param>
/// <param name="Attempt">The attempt number the claim counted for the line.</param>
public sealed record ClaimedOrderLineDto(int ItemId, int Attempt);
