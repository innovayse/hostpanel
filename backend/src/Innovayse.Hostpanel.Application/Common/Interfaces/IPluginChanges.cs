using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The record of the last change made to each plugin and how it ended, kept beside the plugin files so it survives the
/// restart that applied the change. Implemented in Infrastructure, where the plugin folders are.
/// </summary>
public interface IPluginChanges
{
    /// <summary>Reads the last change made to a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The change; <see langword="null"/> when none was recorded.</returns>
    public PluginChangeDto? Find(string pluginId);
}
