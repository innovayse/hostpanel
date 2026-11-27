using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPlugins;

/// <summary>
/// Handles <see cref="ListPluginsQuery"/> by reading the plugins on disk from
/// <see cref="IPluginPackageStore"/>, cross-referencing loaded and refused status from <see cref="IPluginRegistry"/>, the enabled switch from the integration settings
/// and adding the removed plugins whose stored data is still kept.
/// </summary>
public sealed class ListPluginsHandler
{
    /// <summary>Reads the plugins on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Reports which of the plugins on disk were loaded at startup and which were refused.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>Reads which plugin ids still own stored data.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the last change made to each plugin and how it ended.</summary>
    private readonly IPluginChanges _changes;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Reads the plugins on disk.</param>
    /// <param name="registry">Reports which of the plugins on disk were loaded at startup and which were refused.</param>
    /// <param name="context">Reads which plugin ids still own stored data.</param>
    /// <param name="changes">Reads the last change made to each plugin and how it ended.</param>
    public ListPluginsHandler(
        IPluginPackageStore store, IPluginRegistry registry, IHostpanelDbContext context, IPluginChanges changes)
    {
        _store = store;
        _registry = registry;
        _context = context;
        _changes = changes;
    }

    /// <summary>
    /// Returns every plugin on disk with its status and whether it ships with the build (a plugin the loader refused
    /// is a refused row, never loaded), then the removed plugins that still keep data.
    /// </summary>
    /// <param name="query">The list query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rows, always succeeding. A refused row is never loaded.</returns>
    public async Task<Result<IReadOnlyList<PluginListItemDto>>> HandleAsync(
        ListPluginsQuery query, CancellationToken ct)
    {
        var packages = await _store.ListAsync(ct);
        var refused = _registry.GetRefusedPlugins().ToDictionary(r => r.PluginId, StringComparer.OrdinalIgnoreCase);
        var installs = (await _context.InstalledPlugins.AsNoTracking().ToListAsync(ct))
            .ToDictionary(p => p.PluginId, StringComparer.Ordinal);
        var settings = await IntegrationSettingsLookup.LoadAsync(_context, IntegrationSettingKeys.RootPrefix, ct);
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PluginListItemDto>();

        foreach (var package in packages)
        {
            var manifest = package.Manifest;
            shown.Add(manifest.Id);
            shown.Add(package.FolderName);

            // A refusal is recorded under the plugin's folder name. A refused row is addressed by it too, because a
            // removal deletes that folder, and its manifest id may be the very thing that is wrong.
            refused.TryGetValue(package.FolderName, out var refusal);

            // A refused row describes no running package, and a plugin that ships with the build has no install record.
            var install = refusal is null && !package.IsBuiltIn ? installs.GetValueOrDefault(manifest.Id) : null;
            result.Add(new PluginListItemDto(
                refusal is null ? manifest.Id : package.FolderName,
                manifest.Name,
                manifest.Version,
                manifest.Author,
                manifest.Description,
                manifest.Type.ToString().ToLowerInvariant(),
                manifest.Category,
                manifest.Color,
                refusal is null && _registry.IsLoaded(manifest.Id),
                package.IsBuiltIn,
                refusal is null && settings.IsEnabled(manifest.Id),
                refusal is null ? manifest.Capabilities : [],
                refusal is null ? PluginListStatus.Loaded : PluginListStatus.Refused,
                refusal?.Rule,
                refusal?.Detail,
                package.StagedVersion,
                refusal is null && _registry.GetIcon(manifest.Id) is not null ? PluginIconPaths.For(manifest.Id, manifest.Version) : null,
                refusal is null ? manifest.HostAccess : [],
                refusal is null ? manifest.Changelog : null,
                refusal is null ? manifest.MinHostVersion : null,
                refusal is null ? manifest.MaxHostVersion : null,
                refusal is null ? (package.IsBuiltIn ? PluginSource.BuiltIn : install?.Source) : null,
                install?.InstalledAt,
                install?.UpdatedAt,
                install?.InstalledByName,
                install?.Signer,
                install?.SignerKeyId,
                install?.PackageSizeBytes,
                install?.PackageSha256,
                refusal is null ? manifest.SdkPackageVersion.NullIfBlank() : null,
                _changes.Find(package.FolderName)));
        }

        var scopeOwners = _context.PluginDataScopes.AsNoTracking().Select(s => s.PluginId);
        var bindingOwners = _context.PluginSlotProductBindings.AsNoTracking().Select(b => b.PluginId);
        var dataOwners = await scopeOwners.Union(bindingOwners).OrderBy(id => id).ToListAsync(ct);

        // A folder on disk with a corrupt manifest is not in the list above, so the folder itself is asked: it is not removed.
        foreach (var pluginId in dataOwners.Where(id => !shown.Contains(id) && !_store.HasPackageFolder(id)))
        {
            result.Add(RemovedRow(pluginId));
        }

        return Result<IReadOnlyList<PluginListItemDto>>.Success(result);
    }

    /// <summary>Builds the row of a removed plugin that keeps data: only its id, which is also its name, and its status.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The row.</returns>
    private static PluginListItemDto RemovedRow(string pluginId)
    {
        return new PluginListItemDto(
            pluginId, pluginId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            false, false, false, [], PluginListStatus.Removed, HostAccess: []);
    }
}
