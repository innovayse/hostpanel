namespace Innovayse.SDK.Plugins;

/// <summary>
/// Describes a single configuration field exposed by a plugin.
/// </summary>
public sealed class PluginField
{
    /// <summary>Gets or sets the settings-table key for this field.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or sets the human-readable label shown in the admin UI.</summary>
    public required string Label { get; init; }

    /// <summary>Gets or sets the field input type: text | secret | select | textarea.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets a value indicating whether this field must be non-empty before the integration can be enabled.</summary>
    public bool Required { get; init; }
}
