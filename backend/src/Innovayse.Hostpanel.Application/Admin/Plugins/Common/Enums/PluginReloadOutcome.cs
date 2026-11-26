namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>How an attempt to apply a plugin change inside the running process ended.</summary>
public enum PluginReloadOutcome
{
    /// <summary>The change is applied in the running process: the new version is loaded, or the removed plugin is unloaded.</summary>
    Applied,

    /// <summary>The change could not be applied in the running process and takes effect at the next restart.</summary>
    NeedsRestart,

    /// <summary>The new version could not be loaded and the previous version was restored; the host keeps running.</summary>
    Failed,
}
