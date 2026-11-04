using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>Metadata for a plugin that appears in the integrations list.</summary>
/// <param name="Slug">Unique plugin identifier used as URL slug.</param>
/// <param name="Name">Display name shown in the integrations grid.</param>
/// <param name="Category">Category name for grouping (e.g. "Provisioning").</param>
/// <param name="Type">The plugin type its manifest declares; the admin groups and labels the plugin by it.</param>
/// <param name="Color">Brand hex color (e.g. "#1a73e8").</param>
/// <param name="RequiredSettingKeys">
/// Keys of the settings the manifest marks required, which the list checks for "configured". Only keys: the form's
/// labels are resolved per request in the reader's language by the detail read, never once at load (#854).
/// </param>
/// <param name="IconUrl">Path of the admin icon endpoint when the plugin ships an icon; <see langword="null"/> otherwise.</param>
public sealed record PluginIntegrationEntry(
    string Slug,
    string Name,
    string Category,
    PluginType Type,
    string Color,
    IReadOnlyList<string> RequiredSettingKeys,
    string? IconUrl = null);
