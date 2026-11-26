namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>How the last change to an installed plugin ended once the host tried to load it.</summary>
public enum PluginChangeResult
{
    /// <summary>The new version is loaded and running.</summary>
    Applied,

    /// <summary>The new version could not be loaded; the previous version was restored (or, for a new install, left uninstalled).</summary>
    Failed,
}
