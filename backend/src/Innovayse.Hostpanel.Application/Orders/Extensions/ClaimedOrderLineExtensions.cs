using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Orders;

namespace Innovayse.Hostpanel.Application.Orders.Extensions;

/// <summary>The one mapping of claimed order lines to what a run is handed.</summary>
public static class ClaimedOrderLineExtensions
{
    /// <summary>
    /// Maps the lines a claim returned to the lines a run executes, each with the attempt the claim counted. Read after the
    /// claim is saved, so every line has its id.
    /// </summary>
    /// <param name="claimed">The lines <see cref="Order.BeginFulfilment"/> or a retry claimed.</param>
    /// <returns>The claimed lines in id order.</returns>
    public static IReadOnlyList<ClaimedOrderLineDto> ToClaimedLines(this IEnumerable<OrderItem> claimed)
    {
        ArgumentNullException.ThrowIfNull(claimed);

        return claimed
            .OrderBy(i => i.Id)
            .Select(i => new ClaimedOrderLineDto(i.Id, i.FulfilmentAttempts))
            .ToList();
    }
}
