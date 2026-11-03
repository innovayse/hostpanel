namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>What a row of the admin plugin list stands for. Values start at 1.</summary>
public enum PluginListStatus
{
    /// <summary>A plugin that passed every rule: loaded now, or installed and loaded at the next restart.</summary>
    Loaded = 1,

    /// <summary>
    /// A plugin on disk that the host skipped at startup because it breaks a manifest rule. It is never activated and
    /// never reads as enabled; it is listed so it can be fixed or removed.
    /// </summary>
    Refused = 2,

    /// <summary>
    /// A plugin whose package is gone (removed) but whose stored data is still kept; the operator can purge the data.
    /// </summary>
    Removed = 3,
}
