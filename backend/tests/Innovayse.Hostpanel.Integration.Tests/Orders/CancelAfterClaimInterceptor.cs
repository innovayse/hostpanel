using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A save interceptor that cancels the caller's token the moment a save that claims an order line (moves it to in progress)
/// has committed, so a test can cancel a retry exactly between its claim and the run that follows it.
/// </summary>
/// <param name="source">The source whose token the caller passed; cancelled right after the claim is saved.</param>
internal sealed class CancelAfterClaimInterceptor(CancellationTokenSource source) : SaveChangesInterceptor
{
    /// <summary>Whether the save being made claims a line, remembered from before the save to after it.</summary>
    private int _claiming;

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db
            && db.ChangeTracker.Entries<OrderItem>().Any(
                e => e.State == EntityState.Modified && e.Entity.FulfilmentState == OrderItemFulfilmentState.InProgress))
        {
            Interlocked.Exchange(ref _claiming, 1);
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _claiming, 0) == 1)
        {
            source.Cancel();
        }

        return ValueTask.FromResult(result);
    }
}
