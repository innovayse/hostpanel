using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginServers;

/// <summary>
/// Handles <see cref="ListPluginServersQuery"/> from the plugin's manifest and the stored servers. No plugin is built or called, and
/// no credential column is read.
/// </summary>
public sealed class ListPluginServersHandler
{
    /// <summary>The plugins loaded at startup.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>The one reader of the plugins' declared server modules.</summary>
    private readonly IServerModuleCatalog _catalog;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugins loaded at startup.</param>
    /// <param name="catalog">The one reader of the plugins' declared server modules.</param>
    /// <param name="context">The module's data context.</param>
    public ListPluginServersHandler(IPluginRegistry registry, IServerModuleCatalog catalog, IHostpanelDbContext context)
    {
        _registry = registry;
        _catalog = catalog;
        _context = context;
    }

    /// <summary>Lists the servers stored on the modules the plugin declares.</summary>
    /// <param name="query">The plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugin's modules and its servers by name (both empty for a plugin that serves no server module), or <c>PLUGIN_NOT_FOUND</c> when no loaded plugin has that id.</returns>
    public async Task<Result<PluginServersDto>> HandleAsync(ListPluginServersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_registry.IsLoaded(query.PluginId))
        {
            return Result<PluginServersDto>.FailureCode(HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        }

        var modules = _catalog.ModulesOf(query.PluginId);
        if (modules.Count == 0)
        {
            return Result<PluginServersDto>.Success(new PluginServersDto([], []));
        }

        var servers = await _context.Servers.AsNoTracking()
            .OfModules([.. modules])
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .ToOptions()
            .ToListAsync(ct);

        return Result<PluginServersDto>.Success(new PluginServersDto(modules, servers));
    }
}
