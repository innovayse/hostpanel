namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Counts the plugin instances that are in use right now, so a reload can wait for the calls that still run in the old
/// version of a plugin before it unloads the version.
/// </summary>
public sealed class PluginUsage
{
    /// <summary>Guards <see cref="_running"/> and <see cref="_idle"/>.</summary>
    private readonly object _gate = new();

    /// <summary>The instances in use.</summary>
    private int _running;

    /// <summary>Completes when no instance is in use; replaced by an unfinished one while some is.</summary>
    private TaskCompletionSource _idle = Completed();

    /// <summary>Marks one more instance as in use.</summary>
    public void Enter()
    {
        lock (_gate)
        {
            if (_running++ == 0)
            {
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    /// <summary>Marks one instance as no longer in use.</summary>
    public void Leave()
    {
        lock (_gate)
        {
            if (--_running == 0)
            {
                _idle.TrySetResult();
            }
        }
    }

    /// <summary>Waits until no instance is in use.</summary>
    /// <param name="timeout">The longest to wait.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when no instance is in use any more; <see langword="false"/> when the time ran out first.</returns>
    public async Task<bool> WaitForIdleAsync(TimeSpan timeout, CancellationToken ct)
    {
        Task idle;
        lock (_gate)
        {
            idle = _idle.Task;
        }

        try
        {
            await idle.WaitAsync(timeout, ct);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Builds a task source that is already complete: nothing is in use yet.</summary>
    /// <returns>The completed source.</returns>
    private static TaskCompletionSource Completed()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
