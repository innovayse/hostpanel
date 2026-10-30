using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Fulfilment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IFulfilmentPluginResolver"/> that answers with the plugins a test registered, in id order,
/// instead of whatever the host has loaded from its plugin folder. A fake port standing in for the plugin
/// host, which a test must not load a real plugin assembly through.
/// </summary>
public sealed class FakeFulfilmentPluginResolver : IFulfilmentPluginResolver
{
    /// <summary>The plugins this resolver answers with, keyed by plugin id.</summary>
    private readonly SortedDictionary<string, IFulfilmentPlugin> _plugins = new(StringComparer.Ordinal);

    /// <summary>Registers <paramref name="plugin"/> as an enabled fulfilment plugin.</summary>
    /// <param name="pluginId">The plugin id its records name.</param>
    /// <param name="plugin">The plugin to hand back for it.</param>
    public void Register(string pluginId, IFulfilmentPlugin plugin) => _plugins[pluginId] = plugin;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ActiveFulfilmentPlugin>> ResolveAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActiveFulfilmentPlugin>>(
            _plugins.Select(p => new ActiveFulfilmentPlugin(p.Key, p.Value)).ToList());
}
