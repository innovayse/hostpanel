namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>The part of a plugin install, update, removal or restart the host is working on, as the processing dialog reads it.</summary>
public enum PluginChangeStage
{
    /// <summary>The package is being fetched from the registry; the bytes read so far are reported with it.</summary>
    Download,

    /// <summary>The package is being checked: its checksum, its manifest and its signature.</summary>
    Verify,

    /// <summary>The checked package is being put in place beside the running version, or a removed plugin's files are being deleted.</summary>
    Stage,

    /// <summary>The change is being applied in the running process: the plugin is reloaded or unloaded.</summary>
    Apply,

    /// <summary>The host has started its restart to apply the change; the panel waits for it to come back.</summary>
    Restart,
}
