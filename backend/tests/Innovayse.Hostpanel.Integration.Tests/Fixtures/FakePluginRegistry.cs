using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IPluginRegistry"/> that reports whatever manifests a test registered, instead of
/// whatever the test host's plugin folder happens to contain (normally none). A fake port
/// standing in for the on-disk plugin loader, which a test must not depend on.
/// </summary>
public sealed class FakePluginRegistry : IPluginRegistry
{
    /// <summary>The manifests this registry reports as loaded.</summary>
    private readonly List<PluginManifest> _manifests = [];

    /// <summary>The plugins this registry reports as refused for a broken manifest rule.</summary>
    private readonly List<RefusedPlugin> _refused = [];

    /// <summary>Adds <paramref name="plugin"/> to the set this registry reports as refused.</summary>
    /// <param name="plugin">The refused plugin to add.</param>
    public void AddRefused(RefusedPlugin plugin) => _refused.Add(plugin);

    /// <summary>Adds <paramref name="manifest"/> to the set this registry reports as loaded.</summary>
    /// <param name="manifest">The manifest to add.</param>
    public void Add(PluginManifest manifest) => _manifests.Add(manifest);

    /// <inheritdoc/>
    public bool IsMigrationPending(string pluginId) => false;

    /// <inheritdoc/>
    public bool IsLoaded(string pluginId) => _manifests.Exists(m => m.Id == pluginId);

    /// <inheritdoc/>
    public IReadOnlyCollection<PluginManifest> GetLoadedManifests() => _manifests;

    /// <inheritdoc/>
    public IReadOnlyCollection<RefusedPlugin> GetRefusedPlugins() => _refused;
}
