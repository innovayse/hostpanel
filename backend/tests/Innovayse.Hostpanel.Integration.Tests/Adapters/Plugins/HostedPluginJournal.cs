using System.Collections.Concurrent;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Records which test hosted plugins were started and stopped. The runner activates plugins through the
/// container, so the journal reaches them as a constructor dependency, one journal per test.
/// </summary>
internal sealed class HostedPluginJournal
{
    /// <summary>The plugin types whose start completed.</summary>
    private readonly ConcurrentQueue<Type> _started = new();

    /// <summary>The plugin types whose stop was called.</summary>
    private readonly ConcurrentQueue<Type> _stopped = new();

    /// <summary>Gets the plugin types whose start completed, in the order they completed.</summary>
    public IReadOnlyCollection<Type> Started => _started.ToArray();

    /// <summary>Gets the plugin types whose stop was called, in the order they were called.</summary>
    public IReadOnlyCollection<Type> Stopped => _stopped.ToArray();

    /// <summary>Records that a plugin of <paramref name="plugin"/>'s type started.</summary>
    /// <param name="plugin">The plugin that started.</param>
    public void RecordStarted(object plugin) => _started.Enqueue(plugin.GetType());

    /// <summary>Records that a plugin of <paramref name="plugin"/>'s type was asked to stop.</summary>
    /// <param name="plugin">The plugin asked to stop.</param>
    public void RecordStopped(object plugin) => _stopped.Enqueue(plugin.GetType());
}
