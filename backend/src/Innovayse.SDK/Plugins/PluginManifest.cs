namespace Innovayse.SDK.Plugins;

/// <summary>
/// Deserialised representation of a plugin's <c>plugin.json</c> manifest file.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Gets or sets the unique plugin identifier (e.g. "innovayse-cwp").</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the human-readable display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the semver version string.</summary>
    public required string Version { get; init; }

    /// <summary>Gets or sets the author name or organisation.</summary>
    public required string Author { get; init; }

    /// <summary>Gets or sets a short description shown in the admin panel.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or sets the plugin category type.</summary>
    public required PluginType Type { get; init; }

    /// <summary>Gets or sets the display category label (e.g. "Hosting / Provisioning").</summary>
    public required string Category { get; init; }

    /// <summary>Gets or sets the hex colour used for the plugin logo block.</summary>
    public string Color { get; init; } = "#6366f1";

    /// <summary>Gets or sets the fully-qualified type name of the provider implementation.</summary>
    public required string EntryPoint { get; init; }

    /// <summary>Gets or sets the SDK version this plugin was compiled against.</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Gets or sets the list of configuration fields for this plugin.</summary>
    public IReadOnlyList<PluginField> Fields { get; init; } = [];

    /// <summary>Gets a value indicating whether this plugin should appear in the Apps &amp; Integrations list.</summary>
    public bool ShowInIntegrations { get; init; }

    /// <summary>Gets the category name shown in the integrations grid (e.g. "Provisioning").</summary>
    public string? IntegrationsCategory { get; init; }
}
