using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A save interceptor that plays a staff retry at the worst moment: once the first line of a run has stamped the start of its
/// action, it takes over another line of the same order that is still waiting its turn, as a retry would once that line's
/// lease ran out, so a test can prove the first run then leaves that line alone.
/// </summary>
internal sealed class TakeOverOnFirstActionInterceptor : SaveChangesInterceptor
{
    /// <summary>The order and the line whose action start is being saved, remembered from before the save to after it.</summary>
    private (int OrderId, int ItemId)? _stamping;

    /// <summary>Whether the takeover already ran; it runs once.</summary>
    private int _done;

    /// <summary>Gets the line the interceptor took over, or <see langword="null"/> before it did.</summary>
    public int? TakenOverItemId { get; private set; }

    /// <summary>
    /// Gets or sets the services of the host the run goes through; the takeover saves through a context of its own from
    /// there, as a staff request would.
    /// </summary>
    public IServiceProvider? Services { get; set; }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        // The stamp of an action start changes only the time on a line that stays in progress; a claim or an outcome changes
        // its state as well.
        var stamp = eventData.Context?.ChangeTracker.Entries<OrderItem>().FirstOrDefault(e =>
            e.State == EntityState.Modified
            && e.Entity.FulfilmentState == OrderItemFulfilmentState.InProgress
            && !e.Property(i => i.FulfilmentState).IsModified
            && e.Property(i => i.LastFulfilmentAttemptAt).IsModified);
        if (stamp is not null && Volatile.Read(ref _done) == 0)
        {
            _stamping = (stamp.Entity.OrderId, stamp.Entity.Id);
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_stamping is not { } stamping || Services is null || Interlocked.Exchange(ref _done, 1) == 1)
        {
            return result;
        }

        _stamping = null;

        // A retry claims a line whose lease ran out: the moment is a day on, far past any lease, so the claim is allowed.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == stamping.OrderId, cancellationToken);
        var waiting = order.Items.First(i => i.Id != stamping.ItemId && i.FulfilmentState == OrderItemFulfilmentState.InProgress);
        order.StartLineRetry(waiting.Id, DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromMinutes(10));
        TakenOverItemId = waiting.Id;
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
