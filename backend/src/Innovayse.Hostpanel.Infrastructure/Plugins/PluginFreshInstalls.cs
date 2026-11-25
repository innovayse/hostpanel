using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Keeps, per plugin id, the record that a plugin was installed fresh and has not been loaded yet. The host loads a fresh
/// install at its next start, so the record is what tells the loader that a failure to load is the end of a change: the
/// plugin is then taken out again and left uninstalled, instead of staying on disk as a broken plugin.
/// </summary>
/// <remarks>
/// The record is an empty file named after the plugin under <see cref="PluginFolders.Fresh"/>. It is written when the install
/// commits, deleted once the first load of the plugin has ended (either way) and when the plugin is removed.
/// </remarks>
internal static class PluginFreshInstalls
{
    /// <summary>Records that a plugin was installed fresh and waits for its first load.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    public static void Mark(string installRoot, string pluginId)
    {
        Directory.CreateDirectory(Path.Combine(installRoot, PluginFolders.Fresh));
        File.WriteAllBytes(FileOf(installRoot, pluginId), []);
    }

    /// <summary>Tells whether a plugin was installed fresh and has not been loaded yet.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin id; a value that is not a plugin id is never pending.</param>
    /// <returns><see langword="true"/> when the plugin's first load is still to come.</returns>
    public static bool IsPending(string installRoot, string pluginId)
    {
        return PluginIdentifier.IsWellFormed(pluginId) && File.Exists(FileOf(installRoot, pluginId));
    }

    /// <summary>Drops the record of a plugin, if any: once its first load has ended, or when the plugin is removed.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    public static void Clear(string installRoot, string pluginId)
    {
        var file = FileOf(installRoot, pluginId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>The path of a plugin's record.</summary>
    /// <param name="installRoot">The upload folder.</param>
    /// <param name="pluginId">The plugin id, already checked against the plugin id rule.</param>
    /// <returns>The file path.</returns>
    private static string FileOf(string installRoot, string pluginId)
    {
        return Path.Combine(installRoot, PluginFolders.Fresh, pluginId);
    }
}
