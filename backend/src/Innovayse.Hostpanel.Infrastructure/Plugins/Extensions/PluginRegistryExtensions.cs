using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>Extension methods that read the registrar plugins out of the <see cref="PluginRegistry"/>.</summary>
public static class PluginRegistryExtensions
{
    /// <summary>
    /// Finds the loaded registrar plugin a stored module value names. A plugin id is lower case but rows written by
    /// older versions hold the module name in another letter case, so the two are compared without regard to letter case,
    /// which is the one place that rule lives.
    /// </summary>
    /// <param name="registry">The loaded plugins.</param>
    /// <param name="module">The stored module value.</param>
    /// <returns>The plugin, or <see langword="null"/> when no loaded registrar plugin has that id.</returns>
    public static LoadedPlugin? FindRegistrar(this PluginRegistry registry, string module)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(module);

        var id = module.Trim();

        return registry.OfType(PluginType.Registrar)
            .FirstOrDefault(plugin => string.Equals(plugin.Manifest.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
