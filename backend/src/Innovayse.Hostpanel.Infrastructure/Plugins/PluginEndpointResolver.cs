using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Finds the loaded plugin a route belongs to (an app plugin, or any type declaring the <c>endpoints</c> capability)
/// and prepares it through <see cref="PluginActivator"/>, so a plugin reached through a route passes the same gates as
/// every other plugin type: it really implements <see cref="IPluginEndpoints"/> and every capability it declares, the
/// admin switched it on and its required settings are stored.
/// </summary>
public sealed class PluginEndpointResolver : IPluginEndpointResolver
{
    /// <summary>Checks the gates and builds the plugin.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="PluginEndpointResolver"/> class.</summary>
    /// <param name="activator">Checks the gates and builds the plugin.</param>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    public PluginEndpointResolver(PluginActivator activator, PluginRegistry registry)
    {
        _activator = activator;
        _registry = registry;
    }

    /// <inheritdoc/>
    public PluginManifest? Find(string pluginId)
    {
        var plugin = _registry.Find(pluginId);

        // An app plugin, or a plugin of another type that declares the endpoints capability; anything else answers
        // null, so the door's 404 is the same for it as for an unknown id.
        return plugin is not null && plugin.Manifest.ServesEndpoints() ? plugin.Manifest : null;
    }

    /// <inheritdoc/>
    public PluginManifest? FindForCallback(string pluginId)
    {
        var plugin = _registry.Find(pluginId);
        if (plugin is null)
        {
            return null;
        }

        // The loader registers IPluginEndpoints for an app plugin and for any plugin that declares the capability (and
        // refuses one that declares it without implementing it), so the manifest decides here as it did there.
        var served = plugin.Manifest.Type == PluginType.App
            || plugin.Manifest.Capabilities.Contains(PluginCapabilities.Endpoints, StringComparer.Ordinal);
        return served ? plugin.Manifest : null;
    }

    /// <inheritdoc/>
    public async Task<Func<IPluginEndpoints>?> PrepareAsync(string pluginId, CancellationToken ct)
    {
        var plugin = _registry.Find(pluginId);
        if (plugin is null || !plugin.Manifest.ServesEndpoints())
        {
            return null;
        }

        var prepared = await _activator.PrepareAsync<IPluginEndpoints>(plugin, ct);
        return prepared.Outcome == PluginActivationOutcome.Activated ? prepared.Create : null;
    }
}
