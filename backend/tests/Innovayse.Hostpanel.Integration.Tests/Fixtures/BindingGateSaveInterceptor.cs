using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that holds the first save which writes a <see cref="PluginSlotProductBinding"/> at the door until
/// the test lets it through. By then the handler has read its rows and holds the widget lock, so a test can start a
/// second save of the same widget and decide, without timing luck, how the two overlap.
/// </summary>
internal sealed class BindingGateSaveInterceptor : SaveChangesInterceptor
{
    /// <summary>Completed when the first save reaches the door.</summary>
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completed by the test to let the held save go on.</summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Whether a save was already held; only the first is.</summary>
    private int _held;

    /// <summary>Gets a task that completes when a save is waiting at the door.</summary>
    public Task Entered => _entered.Task;

    /// <summary>Lets the held save go on.</summary>
    public void Release()
    {
        _release.TrySetResult();
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var writes = eventData.Context is { } db && db.ChangeTracker.Entries<PluginSlotProductBinding>().Any(e => e.State == EntityState.Added);
        if (writes && Interlocked.Exchange(ref _held, 1) == 0)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
