namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The names of the working folders inside the upload folder. Each starts with a dot, which fails the plugin id rule,
/// so the loader and the plugin list never take one for a plugin.
/// </summary>
internal static class PluginFolders
{
    /// <summary>The manifest file name at the root of every plugin archive and folder.</summary>
    public const string ManifestFileName = "plugin.json";

    /// <summary>Holds, per plugin id, an upgrade that waits for the next restart to replace the installed plugin.</summary>
    public const string Staged = ".staged";

    /// <summary>Holds, per plugin id, the record of a swapped upgrade whose migration hook has not run yet.</summary>
    public const string Upgrades = ".upgrades";

    /// <summary>Scratch space for an install and for folders being deleted.</summary>
    public const string Scratch = ".staging";
}
