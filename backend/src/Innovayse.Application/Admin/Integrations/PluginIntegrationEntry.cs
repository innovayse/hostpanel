namespace Innovayse.Application.Admin.Integrations;

using Innovayse.Application.Admin.Integrations.Common;

/// <summary>Metadata for a plugin that appears in the integrations list.</summary>
/// <param name="Slug">Unique plugin identifier used as URL slug.</param>
/// <param name="Name">Display name shown in the integrations grid.</param>
/// <param name="Category">Category name for grouping (e.g. "Provisioning").</param>
/// <param name="Color">Brand hex color (e.g. "#1a73e8").</param>
/// <param name="FieldDefinitions">Config field definitions for the dynamic form.</param>
public record PluginIntegrationEntry(
    string Slug,
    string Name,
    string Category,
    string Color,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions);
