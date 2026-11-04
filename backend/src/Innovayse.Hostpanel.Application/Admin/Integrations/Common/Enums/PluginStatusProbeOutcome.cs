namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;

/// <summary>How a status check of a plugin (<c>IPluginStatus</c>) ended. Values start at 1; 0 is not an outcome.</summary>
public enum PluginStatusProbeOutcome
{
    /// <summary>The plugin answered with a report; its health is in the result.</summary>
    Reported = 1,

    /// <summary>The plugin is not loaded or does not implement <c>IPluginStatus</c>, so it cannot be asked.</summary>
    NotSupported = 2,

    /// <summary>The plugin is switched off, misses a required setting or waits for its data migration; it was not called.</summary>
    Inactive = 3,

    /// <summary>The plugin threw, or answered with something that is not a report. Fail closed: read as not working.</summary>
    Failed = 4,

    /// <summary>The plugin did not answer within the host's time limit. Fail closed: read as not working.</summary>
    TimedOut = 5,
}
