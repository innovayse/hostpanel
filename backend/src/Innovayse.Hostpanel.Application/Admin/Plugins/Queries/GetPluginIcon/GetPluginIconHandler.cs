using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetPluginIcon;

/// <summary>
/// Handles <see cref="GetPluginIconQuery"/> from the icon the registry kept when the plugin was loaded. No file is read
/// and no plugin is built or called.
/// </summary>
public sealed class GetPluginIconHandler
{
    /// <summary>The plugins loaded at startup and the icons they shipped.</summary>
    private readonly IPluginRegistry _registry;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugins loaded at startup and the icons they shipped.</param>
    public GetPluginIconHandler(IPluginRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Returns the icon of a loaded plugin.</summary>
    /// <param name="query">The plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The icon, or <c>PLUGIN_ICON_NOT_FOUND</c> when no loaded plugin has that id or the plugin declares no icon.</returns>
    public Task<Result<PluginIconDto>> HandleAsync(GetPluginIconQuery query, CancellationToken ct)
    {
        var icon = _registry.GetIcon(query.PluginId);
        return Task.FromResult(icon is null
            ? Result<PluginIconDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ICON_NOT_FOUND)
            : Result<PluginIconDto>.Success(icon));
    }
}
