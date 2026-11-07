namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>
/// Where the plugin registry is and how far the host trusts what it sends, bound from the <c>PluginRegistry</c> section.
/// There is no default address: the registry is off until an operator sets <see cref="BaseUrl"/> and
/// <see cref="Enabled"/>, so a fresh installation makes no outbound call to a catalogue it was never pointed at.
/// </summary>
public sealed class PluginRegistryOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PluginRegistry";

    /// <summary>The smallest time limit the validator accepts.</summary>
    public static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The largest time limit the validator accepts.</summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The largest download limit the validator accepts, in bytes (100 MiB).</summary>
    public const long MaxAllowedDownloadBytes = 104_857_600;

    /// <summary>Whether the registry is used. Off by default; an install and a listing answer <c>PLUGIN_REGISTRY_DISABLED</c> while it is off.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The registry's address, an absolute <c>https</c> URL such as <c>https://registry.example.com/api/</c>. Packages
    /// are only ever downloaded from this URL's host. Empty until the operator sets it; required when <see cref="Enabled"/>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The time limit of one registry call, a download included.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The most bytes a package download may hold; a larger package is refused (default 50 MiB, the upload limit).</summary>
    public long MaxDownloadBytes { get; set; } = 52_428_800;
}
