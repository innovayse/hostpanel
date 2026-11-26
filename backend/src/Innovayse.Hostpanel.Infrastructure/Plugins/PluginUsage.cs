namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Counts the plugin instances that are in use right now, so a reload can wait for the calls that still run in the old
/// version of a plugin before it unloads the version. A drain closes the counter first, under the same lock that admits a new
/// instance, so no instance can be built from a version after the drain found it idle.
/// </summary>
public sealed class PluginUsage
{
    /// <summary>Guards <see cref="_running"/>, <see cref="_idle"/> and <see cref="_closed"/>.</summary>
    private readonly object _gate = new();

    /// <summary>The instances in use.</summary>
    private int _running;

    /// <summary>Completes when no instance is in use; replaced by an unfinished one while some is.</summary>
    private TaskCompletionSource _idle = Completed();

    /// <summary>True while a drain holds the version closed: no new instance may be built from it.</summary>
    private bool _closed;

    /// <summary>Marks one more instance as in use, unless a drain has closed the version.</summary>
    /// <returns><see langword="true"/> when the instance may be built; <see langword="false"/> while the version is closed.</returns>
    public bool TryEnter()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return false;
            }

            if (_running++ == 0)
            {
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return true;
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

    /// <summary>
    /// Closes the version to new instances and waits until no instance is in use. When the time runs out the version is
    /// opened again, so it goes on exactly as before.
    /// </summary>
    /// <param name="timeout">The longest to wait.</param>
    /// <returns><see langword="true"/> when no instance is in use any more and the version stays closed; <see langword="false"/> when the time ran out first.</returns>
    public async Task<bool> DrainAsync(TimeSpan timeout)
    {
        Task idle;
        lock (_gate)
        {
            _closed = true;
            idle = _idle.Task;
        }

        try
        {
            await idle.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            lock (_gate)
            {
                _closed = false;
            }

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
