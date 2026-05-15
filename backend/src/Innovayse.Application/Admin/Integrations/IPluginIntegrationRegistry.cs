namespace Innovayse.Application.Admin.Integrations;

using Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>Registry of plugins that expose themselves as integrations in the admin UI.</summary>
public interface IPluginIntegrationRegistry
{
    /// <summary>Registers a plugin as an integration entry.</summary>
    /// <param name="entry">The plugin integration metadata.</param>
    void Register(PluginIntegrationEntry entry);

    /// <summary>Returns all registered plugin integration entries.</summary>
    /// <returns>Read-only list of plugin integration entries.</returns>
    IReadOnlyList<PluginIntegrationEntry> GetAll();

    /// <summary>Returns true if a plugin with the given slug is registered.</summary>
    /// <param name="slug">The plugin identifier slug.</param>
    /// <returns>True if registered, false otherwise.</returns>
    bool IsRegistered(string slug);
}

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
