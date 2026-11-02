using Innovayse.SDK.Plugins.Store;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The plugin store of a call before the host keeps any: every member says so by throwing, which the plugin runner
/// answers as <c>PLUGIN_FAILED</c> and logs with the plugin id. A store that accepted writes and lost them, or read
/// as empty, would let a plugin believe its state is saved.
/// </summary>
public sealed class UnavailablePluginStore : IPluginStore
{
    /// <summary>The sentence every member throws with.</summary>
    public const string Reason = "The host does not keep plugin data yet.";

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host keeps no plugin data yet.</exception>
    public Task<string?> GetAsync(string key, CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host keeps no plugin data yet.</exception>
    public Task SetAsync(string key, string value, CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host keeps no plugin data yet.</exception>
    public Task<bool> RemoveAsync(string key, CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }
}
