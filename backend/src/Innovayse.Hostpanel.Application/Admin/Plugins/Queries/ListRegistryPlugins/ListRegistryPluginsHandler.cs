using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListRegistryPlugins;

/// <summary>
/// Handles <see cref="ListRegistryPluginsQuery"/>: asks the registry for a page and sets it beside the plugins
/// installed on this server.
/// </summary>
public sealed class ListRegistryPluginsHandler
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the plugins installed on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="store">Reads the plugins installed on disk.</param>
    public ListRegistryPluginsHandler(IPluginRegistryClient registry, IPluginPackageStore store)
    {
        _registry = registry;
        _store = store;
    }

    /// <summary>Returns one page of registry plugins with their installed state.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The page; or the registry's <c>PLUGIN_REGISTRY_*</c> failure (<c>PLUGIN_REGISTRY_DISABLED</c> when the registry is
    /// off, <c>PLUGIN_REGISTRY_UNAVAILABLE</c>, <c>PLUGIN_REGISTRY_TIMED_OUT</c> or <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c>
    /// when it cannot be used).
    /// </returns>
    public async Task<Result<RegistryPluginPageDto>> HandleAsync(ListRegistryPluginsQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);
        var page = Math.Max(1, query.Page);

        var listing = await _registry.SearchAsync(
            new RegistryPluginSearch(NullIfBlank(query.Q), query.Category, page, pageSize), ct);
        if (listing.IsFailure)
        {
            return Result<RegistryPluginPageDto>.FailureCode(listing.ErrorCode!);
        }

        var installed = (await _store.ListAsync(ct))
            .GroupBy(p => p.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var rows = listing.Value!.Items.Select(entry =>
        {
            if (!installed.TryGetValue(entry.Id, out var package))
            {
                return entry;
            }

            var update = PluginUpdateRule.IsUpdate(package.Manifest.Version, package.StagedVersion, entry.LatestVersion);
            return entry with { Installed = true, InstalledVersion = package.Manifest.Version, UpdateAvailable = update };
        }).ToList();

        return Result<RegistryPluginPageDto>.Success(new RegistryPluginPageDto(rows, listing.Value.Total, page, pageSize));
    }

    /// <summary>Turns a blank filter into no filter.</summary>
    /// <param name="value">The filter text.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when blank.</returns>
    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
