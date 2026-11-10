using Innovayse.Hostpanel.Application.Orders.Common;

namespace Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;

/// <summary>
/// Fulfils every line of an accepted order the way its type asks: registers, transfers or renews a
/// domain line, creates a client service for a product line, and links a new domain to a new service
/// bought together. Shared by admin acceptance, paid-order fulfilment and the retry of one line, never sent by
/// a controller. Each line's outcome is recorded on the line, and a line already fulfilled is never fulfilled again.
/// </summary>
/// <param name="OrderId">The accepted order whose lines are fulfilled.</param>
/// <param name="ItemId">
/// The one line to fulfil, as a retry of it; <see langword="null"/> fulfils every line that is not fulfilled yet.
/// A retry has already claimed the line, so its attempt is not counted a second time.
/// </param>
/// <param name="Claimed">
/// The lines the sender already claimed, with the attempt each was given (acceptance claims in the save that accepts the
/// order, a retry claims its one line); exactly these run. <see langword="null"/> has the run claim the lines itself.
/// </param>
public sealed record FulfillOrderLinesCommand(int OrderId, int? ItemId = null, IReadOnlyList<ClaimedOrderLineDto>? Claimed = null);
