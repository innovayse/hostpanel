using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>Metadata for a plugin that appears in the integrations list.</summary>
/// <param name="Slug">Unique plugin identifier used as URL slug.</param>
/// <param name="Name">Display name shown in the integrations grid.</param>
/// <param name="Category">Category name for grouping (e.g. "Provisioning").</param>
/// <param name="Type">The plugin type its manifest declares; the admin groups and labels the plugin by it.</param>
/// <param name="Color">Brand hex color (e.g. "#1a73e8").</param>
/// <param name="FieldDefinitions">Config field definitions for the dynamic form.</param>
/// <param name="IconUrl">Path of the admin icon endpoint when the plugin ships an icon; <see langword="null"/> otherwise.</param>
public sealed record PluginIntegrationEntry(
    string Slug,
    string Name,
    string Category,
    PluginType Type,
    string Color,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions,
    string? IconUrl = null);
