namespace Innovayse.Hostpanel.Domain.Plugins.Enums;

/// <summary>How an uploaded plugin version relates to the installed one, as <see cref="PluginVersion.Compare"/> decides it.</summary>
public enum PluginVersionChange
{
    /// <summary>The uploaded version has higher semantic version precedence than the installed one.</summary>
    Newer = 1,

    /// <summary>The two versions have equal precedence (build metadata is ignored).</summary>
    Same = 2,

    /// <summary>The uploaded version has lower semantic version precedence than the installed one.</summary>
    Older = 3,

    /// <summary>One of the two texts is not a valid version, so neither an upgrade nor a downgrade can be proven.</summary>
    Incomparable = 4,
}
