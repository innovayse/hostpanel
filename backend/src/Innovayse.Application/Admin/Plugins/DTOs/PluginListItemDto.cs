namespace Innovayse.Application.Admin.Plugins.DTOs;

/// <summary>
/// Summary item for one installed plugin, returned by the list endpoint.
/// </summary>
/// <param name="Id">Unique plugin identifier from <c>plugin.json</c>.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Version">Semver version string.</param>
/// <param name="Author">Author name or organisation.</param>
/// <param name="Description">Short description shown in the admin panel.</param>
/// <param name="Type">Plugin type string: provisioning | payment | registrar.</param>
/// <param name="Category">Display category label, e.g. "Hosting / Provisioning".</param>
/// <param name="Color">Hex colour for the plugin logo block.</param>
/// <param name="IsLoaded">Whether the DLL was successfully loaded at startup.</param>
public record PluginListItemDto(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Type,
    string Category,
    string Color,
    bool IsLoaded);
