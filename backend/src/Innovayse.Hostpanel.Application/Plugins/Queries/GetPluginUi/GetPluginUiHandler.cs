using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginUi;

/// <summary>
/// Handles <see cref="GetPluginUiQuery"/>: the pages the caller may open in the panel, of every available plugin, and the
/// menu entries that lead to them, every label in the request's language. A menu entry is listed only beside a visible
/// page, so it never leads to a refusal. The client area filters by the caller's permissions; the admin area by none.
/// Pages follow the source's order, plugin id (ordinal), then manifest order; menus by order, then plugin id, then menu id.
/// </summary>
public sealed class GetPluginUiHandler
{
    /// <summary>The plugins whose pages the panels may show.</summary>
    private readonly IPluginUiSource _source;

    /// <summary>Resolves the account the caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Initializes the handler.</summary>
    /// <param name="source">The plugins whose pages the panels may show.</param>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    public GetPluginUiHandler(IPluginUiSource source, ICallerClientResolver callerClient)
    {
        _source = source;
        _callerClient = callerClient;
    }

    /// <summary>Builds the panel's plugin index for the caller.</summary>
    /// <param name="query">The area to list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The index, empty lists when no plugin is available; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could
    /// not say who a client caller is; <c>CLIENT_PROFILE_NOT_FOUND</c> when a client caller acts for no account.
    /// </returns>
    public async Task<Result<PluginUiDto>> HandleAsync(GetPluginUiQuery query, CancellationToken ct)
    {
        var held = await PluginUiViewer.PermissionsAsync(query.Area, _callerClient, ct);
        if (held.IsFailure)
        {
            // Named explicitly rather than forwarded: the viewer refuses with exactly these two codes, and an index never
            // answers a code this handler did not decide on.
            return Result<PluginUiDto>.FailureCode(held.ErrorCode == HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE
                ? HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE
                : HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        var menus = new List<PluginUiMenuItemDto>();
        var pages = new List<PluginUiPageDto>();
        // Not sorted again here: IPluginUiSource is the ordering contract (by plugin id, ordinal), so the index is the
        // same on every instance whatever order the plugins were loaded in, and pages keep manifest order within a plugin.
        foreach (var package in await _source.ListAvailableAsync(ct))
        {
            var manifest = package.Manifest;
            var area = query.Area == PluginArea.Admin ? manifest.Ui.Admin : manifest.Ui.Client;
            var texts = PluginUiTexts.For(manifest);
            var visible = area.Pages.Where(page => PluginClientPermissions.IsHeld(held.Value, page.Permission)).ToList();
            pages.AddRange(visible.Select(page => new PluginUiPageDto(manifest.Id, page.Id, page.Path, texts.Resolve(page.Title))));
            menus.AddRange(area.Menus
                .Where(item => visible.Exists(page => string.Equals(page.Id, item.Page, StringComparison.Ordinal)))
                .Select(item => new PluginUiMenuItemDto(manifest.Id, item.Id, texts.Resolve(item.Label), item.Page, item.Icon, item.Order)));
        }

        var ordered = menus
            .OrderBy(item => item.Order)
            .ThenBy(item => item.PluginId, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        return Result<PluginUiDto>.Success(new PluginUiDto(ordered, pages));
    }
}
