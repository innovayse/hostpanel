namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The plugin instances one scope built, released when the scope ends. A plugin instance lives as long as the scope that
/// built it (a request, a streamed call, a job pass), so the end of the scope is the moment its calls are over.
/// </summary>
public sealed class PluginUsageLeases : IDisposable
{
    /// <summary>Guards <see cref="_held"/>: a scope can build plugins from several tasks.</summary>
    private readonly object _gate = new();

    /// <summary>The usage counters this scope entered and has not left.</summary>
    private readonly List<PluginUsage> _held = [];

    /// <summary>Marks a plugin instance as in use until the scope ends, unless a reload has closed its version.</summary>
    /// <param name="usage">The usage counter of the plugin version the instance is built from.</param>
    /// <returns><see langword="true"/> when the instance may be built; <see langword="false"/> while the version drains.</returns>
    public bool TryHold(PluginUsage usage)
    {
        lock (_gate)
        {
            if (!usage.TryEnter())
            {
                return false;
            }

            _held.Add(usage);
            return true;
        }
    }

    /// <summary>Releases every instance the scope built.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var usage in _held)
            {
                usage.Leave();
            }

            _held.Clear();
        }
    }
}
