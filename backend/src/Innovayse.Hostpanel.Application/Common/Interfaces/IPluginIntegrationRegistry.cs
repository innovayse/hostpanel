using Innovayse.Hostpanel.Application.Admin.Integrations.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Registry of plugins that expose themselves as integrations in the admin UI.</summary>
public interface IPluginIntegrationRegistry
{
    /// <summary>Registers a plugin as an integration entry, replacing the entry the plugin already has.</summary>
    /// <param name="entry">The plugin integration metadata.</param>
    public void Register(PluginIntegrationEntry entry);

    /// <summary>Forgets the integration entry of a plugin that was unloaded; a slug that is not registered is ignored.</summary>
    /// <param name="slug">The plugin identifier slug.</param>
    public void Remove(string slug);

    /// <summary>Returns all registered plugin integration entries.</summary>
    /// <returns>Read-only list of plugin integration entries.</returns>
    public IReadOnlyList<PluginIntegrationEntry> GetAll();

    /// <summary>Returns true if a plugin with the given slug is registered.</summary>
    /// <param name="slug">The plugin identifier slug.</param>
    /// <returns>True if registered, false otherwise.</returns>
    public bool IsRegistered(string slug);
}
