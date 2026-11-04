namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Counts, per plugin, the calls the host gave up waiting for but whose work is still running because the plugin ignored its
/// cancellation token. A singleton: the count outlives the request scope the abandoned call came from. While a plugin holds
/// as many as the limit allows, <see cref="PluginCallRunner"/> refuses new calls to it at once, so a plugin that never ends
/// its work cannot pile up unbounded work on the host.
/// </summary>
public sealed class PluginAbandonedCalls
{
    /// <summary>Guards <see cref="_counts"/>.</summary>
    private readonly object _gate = new();

    /// <summary>The abandoned calls still running, by plugin id; a plugin with none has no entry.</summary>
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>Gets how many abandoned calls of a plugin are still running.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The count; zero for a plugin with none.</returns>
    public int CountOf(string pluginId)
    {
        lock (_gate)
        {
            return _counts.TryGetValue(pluginId, out var count) ? count : 0;
        }
    }

    /// <summary>Whether a plugin may be called: it holds fewer abandoned calls than the limit.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="limit">The most abandoned calls a plugin may hold.</param>
    /// <returns><see langword="true"/> when a new call may run.</returns>
    public bool HasRoom(string pluginId, int limit)
    {
        return CountOf(pluginId) < limit;
    }

    /// <summary>Counts a call as abandoned until its work ends, however it ends.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="work">The work the host stopped waiting for.</param>
    public void Track(string pluginId, Task work)
    {
        ArgumentNullException.ThrowIfNull(work);

        lock (_gate)
        {
            _counts[pluginId] = (_counts.TryGetValue(pluginId, out var count) ? count : 0) + 1;
        }

        // Observes the failure too, so an abandoned call's eventual exception is not reported as unobserved.
        _ = work.ContinueWith(
            static (step, state) =>
            {
                _ = step.Exception;
                var (owner, id) = ((PluginAbandonedCalls Owner, string Id))state!;
                owner.Release(id);
            },
            (this, pluginId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Takes one abandoned call off a plugin's count once its work has ended.</summary>
    /// <param name="pluginId">The plugin.</param>
    private void Release(string pluginId)
    {
        lock (_gate)
        {
            if (!_counts.TryGetValue(pluginId, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _counts.Remove(pluginId);
            }
            else
            {
                _counts[pluginId] = count - 1;
            }
        }
    }
}
