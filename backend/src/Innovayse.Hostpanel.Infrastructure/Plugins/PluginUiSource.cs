using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Answers which plugins' pages the panels may show from the loaded plugins, through the same
/// <see cref="PluginActivator"/> gates a plugin route passes, so a page is listed exactly when the routes it calls answer.
/// Nothing of the plugin runs here: the gates stop before its constructor. The gates' answer for every app plugin is
/// computed in one pass and remembered by <see cref="PluginAvailabilityCache"/>, so the index and the page lookup read
/// the same set and an unknown id costs exactly what an installed one does.
/// </summary>
public sealed class PluginUiSource : IPluginUiSource
{
    /// <summary>Checks that a plugin is switched on and configured.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Remembers the available set for a few seconds, shared by every request.</summary>
    private readonly PluginAvailabilityCache _cache;

    /// <summary>Initializes a new instance of the <see cref="PluginUiSource"/> class.</summary>
    /// <param name="activator">Checks that a plugin is switched on and configured.</param>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    /// <param name="cache">Remembers the available set for a few seconds, shared by every request.</param>
    public PluginUiSource(PluginActivator activator, PluginRegistry registry, PluginAvailabilityCache cache)
    {
        _activator = activator;
        _registry = registry;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginUiPackage>> ListAvailableAsync(CancellationToken ct)
    {
        var available = await _cache.GetOrComputeAsync(ComputeAvailableAsync, ct);

        // Ordered by id here, ordinal: the listing order must not depend on the registry's or a dictionary's order,
        // which changes with load order and would reorder the panels' menus between restarts.
        return _registry.OfType(PluginType.App)
            .Where(plugin => available.Contains(plugin.Manifest.Id))
            .OrderBy(plugin => plugin.Manifest.Id, StringComparer.Ordinal)
            .Select(plugin => new PluginUiPackage(plugin.Manifest, plugin.Views))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<PluginUiPackage?> FindAvailableAsync(string pluginId, CancellationToken ct)
    {
        // The set is read first, whatever the id: an unknown id waits for the same pass an installed one does, so the
        // time to answer says nothing about which ids exist.
        var available = await _cache.GetOrComputeAsync(ComputeAvailableAsync, ct);
        if (!available.Contains(pluginId))
        {
            return null;
        }

        var plugin = _registry.Find(pluginId);
        return plugin is { Manifest.Type: PluginType.App } ? new PluginUiPackage(plugin.Manifest, plugin.Views) : null;
    }

    /// <summary>Runs the activation gates of every loaded app plugin, in one pass, without building any of them.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the app plugins that implement <see cref="IPluginEndpoints"/>, are switched on and are configured.</returns>
    private async Task<IReadOnlySet<string>> ComputeAvailableAsync(CancellationToken ct)
    {
        var available = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in _registry.OfType(PluginType.App))
        {
            var prepared = await _activator.PrepareAsync<IPluginEndpoints>(plugin, ct);
            if (prepared.Outcome == PluginActivationOutcome.Activated)
            {
                available.Add(plugin.Manifest.Id);
            }
        }

        return available;
    }
}
