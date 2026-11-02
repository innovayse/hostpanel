using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Finds the loaded app plugin a route belongs to and prepares it through <see cref="PluginActivator"/>, so a plugin
/// reached through a route passes the same gates as every other plugin type: it really implements
/// <see cref="IPluginEndpoints"/>, the admin switched it on and its required settings are stored.
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

        // Only an app plugin answers routes: the loader registers IPluginEndpoints for no other type.
        return plugin is { Manifest.Type: PluginType.App } ? plugin.Manifest : null;
    }

    /// <inheritdoc/>
    public async Task<Func<IPluginEndpoints>?> PrepareAsync(string pluginId, CancellationToken ct)
    {
        var plugin = _registry.Find(pluginId);
        if (plugin is null)
        {
            return null;
        }

        var prepared = await _activator.PrepareAsync<IPluginEndpoints>(plugin, ct);
        return prepared.Outcome == PluginActivationOutcome.Activated ? prepared.Create : null;
    }
}
