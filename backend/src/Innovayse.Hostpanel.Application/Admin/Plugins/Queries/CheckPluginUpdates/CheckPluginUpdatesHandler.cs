using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.CheckPluginUpdates;

/// <summary>
/// Handles <see cref="CheckPluginUpdatesQuery"/>: asks the registry for the newest version of every uploaded plugin
/// and keeps the ones that are newer than what is installed. Plugins shipped with the build are not asked about,
/// because an upload cannot replace them.
/// </summary>
public sealed class CheckPluginUpdatesHandler
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the plugins installed on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="store">Reads the plugins installed on disk.</param>
    public CheckPluginUpdatesHandler(IPluginRegistryClient registry, IPluginPackageStore store)
    {
        _registry = registry;
        _store = store;
    }

    /// <summary>Returns the installed plugins that have a newer version in the registry.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updates (the same rows as the browser, installed and update flags set), in plugin id order (empty when nothing uploaded is installed, without asking the registry); or the
    /// registry's <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public async Task<Result<IReadOnlyList<RegistryPluginDto>>> HandleAsync(CheckPluginUpdatesQuery query, CancellationToken ct)
    {
        var uploaded = (await _store.ListAsync(ct))
            .Where(p => !p.IsBuiltIn)
            .GroupBy(p => p.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        if (uploaded.Count == 0)
        {
            return Result<IReadOnlyList<RegistryPluginDto>>.Success([]);
        }

        var latest = await _registry.GetLatestAsync(uploaded.Keys.ToList(), ct);
        if (latest.IsFailure)
        {
            return Result<IReadOnlyList<RegistryPluginDto>>.FailureCode(latest.ErrorCode!);
        }

        var updates = new List<RegistryPluginDto>();
        foreach (var entry in latest.Value!.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            // The registry may answer for an id that was not asked about; only installed plugins are reported.
            if (!uploaded.TryGetValue(entry.Id, out var package))
            {
                continue;
            }

            if (PluginUpdateRule.IsUpdate(package.Manifest.Version, package.StagedVersion, entry.LatestVersion))
            {
                updates.Add(entry with { Installed = true, InstalledVersion = package.Manifest.Version, UpdateAvailable = true });
            }
        }

        return Result<IReadOnlyList<RegistryPluginDto>>.Success(updates);
    }
}
