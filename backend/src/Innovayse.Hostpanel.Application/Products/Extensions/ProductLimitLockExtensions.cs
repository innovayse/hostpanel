using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The lock that serialises everything that takes a place of a product with a cap or a one-per-client rule, so the
/// count the check reads and the order or service that then takes the place cannot interleave with another caller's.
/// </summary>
public static class ProductLimitLockExtensions
{
    /// <summary>
    /// Takes the transaction-scoped lock of each product's places. Must run inside a transaction started on the same
    /// context, and be held until the order or service that takes the place is committed: a parallel caller then waits
    /// for it and counts the place it took.
    /// </summary>
    /// <param name="context">The context whose transaction holds the lock.</param>
    /// <param name="productIds">The products with a cap or a one-per-client rule; duplicates are locked once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every lock is held.</returns>
    public static async Task LockProductPlacesAsync(
        this IHostpanelDbContext context, IEnumerable<int> productIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(productIds);

        // Ascending, so two callers locking overlapping sets queue in the same order and never deadlock.
        foreach (var productId in productIds.Distinct().Order())
        {
            await context.LockForTransactionAsync($"product-places:{productId}", ct);
        }
    }
}
