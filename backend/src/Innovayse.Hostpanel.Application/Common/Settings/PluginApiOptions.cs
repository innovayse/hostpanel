namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>
/// What the host allows a call to a plugin route to carry. Bound from the "PluginApi" section; a deployment sets
/// it only to change the defaults.
/// </summary>
public sealed class PluginApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PluginApi";

    /// <summary>The largest request body the host reads for a plugin, in bytes (1 MiB by default).</summary>
    public const int DefaultMaxBodyBytes = 1_048_576;

    /// <summary>The largest <see cref="MaxBodyBytes"/> a deployment may configure, in bytes (64 MiB).</summary>
    public const int MaxBodyBytesCeiling = 67_108_864;

    /// <summary>
    /// The most bytes of request body a plugin route accepts. A call that declares more, or sends more, is refused
    /// with <c>PLUGIN_REQUEST_TOO_LARGE</c> (413) before the plugin is called. The plugin sees the whole body or none of it.
    /// </summary>
    public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

    /// <summary>Whether the values can bound a body: at least one byte and no more than <see cref="MaxBodyBytesCeiling"/>.</summary>
    public bool IsValid => MaxBodyBytes is >= 1 and <= MaxBodyBytesCeiling;
}
