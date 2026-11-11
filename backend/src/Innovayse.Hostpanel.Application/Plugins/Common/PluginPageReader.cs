using Hostpanel.Sdk.Packaging.Naming;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

/*
 * The one body behind every way a panel opens a plugin page (by id, by path). Both lookups must refuse in the same
 * order and with the same body, or the second door becomes an oracle for what the first one hides: a page reachable
 * by path but answered differently from the same page by id would tell a caller that it exists. So the steps live
 * here once and a use case only says how to pick the page out of the area's declared pages.
 */

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Reads one plugin page of a panel for the caller: the plugin id's shape, then the viewer (the account a client caller
/// acts for and the permissions held there), then the available plugin, the page the caller's selector picks, the
/// page's permission and the localised view. Every refusal is the same <c>PLUGIN_PAGE_NOT_FOUND</c> except the identity
/// plugin's outage.
/// </summary>
public sealed class PluginPageReader
{
    /// <summary>The plugins whose pages the panels may show.</summary>
    private readonly IPluginUiSource _source;

    /// <summary>Resolves the account the caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Works out which origins a preview may frame for the caller.</summary>
    private readonly PluginFrameOriginResolver _frameOrigins;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">The plugins whose pages the panels may show.</param>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    /// <param name="origins">Works out the origins a view may frame or link to for the caller.</param>
    public PluginPageReader(IPluginUiSource source, ICallerClientResolver callerClient, PluginFrameOriginResolver origins)
    {
        _source = source;
        _callerClient = callerClient;
        _frameOrigins = origins;
    }

    /// <summary>Reads and localises the page <paramref name="select"/> picks from the plugin's pages of the area.</summary>
    /// <param name="area">The panel: client or admin.</param>
    /// <param name="pluginId">The plugin id from the URL, not yet checked.</param>
    /// <param name="select">
    /// Picks the requested page out of the pages the plugin declares in <paramref name="area"/>, in manifest order;
    /// <see langword="null"/> when none is the requested one.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The page; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who a client caller is;
    /// <c>PLUGIN_PAGE_NOT_FOUND</c> for every other refusal: a plugin id that is not well formed, a plugin that is not
    /// available, no page picked, a client caller who acts for no account or lacks the page's permission, or a missing
    /// view.
    /// </returns>
    public async Task<Result<PluginPageViewDto>> ReadAsync(
        PluginArea area,
        string pluginId,
        Func<IReadOnlyList<PluginPage>, PluginPage?> select,
        CancellationToken ct)
    {
        // An id that cannot name any plugin is refused before anything is looked up, with the very answer an unknown
        // page gets: a 400 would tell a caller which ids are well formed. The shape is public (it is the manifest rule),
        // so answering it ahead of the identity plugin's outage reveals nothing about what is installed.
        if (!PluginIdentifier.IsWellFormed(pluginId))
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }

        // The viewer comes first so the lookups below run for every caller alike; only a dependency outage, which says
        // nothing about the page, answers differently from "not found".
        var held = await PluginUiViewer.PermissionsAsync(area, _callerClient, ct);
        if (held.IsFailure && held.ErrorCode == HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE)
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        var package = await _source.FindAvailableAsync(pluginId, ct);
        var page = package is null
            ? null
            : select((area == PluginArea.Admin ? package.Manifest.Ui.Admin : package.Manifest.Ui.Client).Pages);

        if (package is null
            || page is null
            || held.IsFailure
            || !PluginClientPermissions.IsHeld(held.Value, page.Permission))
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }

        var manifest = package.Manifest;
        var texts = PluginUiTexts.For(manifest);

        // The view was read and checked when the plugin loaded, so a page always has one; reading it here keeps the
        // types honest rather than asserting it.
        if (package.Views.Localised(page.View, texts) is not { } view)
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }

        // The admin panel renders no preview and acts for no client, so nothing is read for it.
        IReadOnlyList<string> frameOrigins = area == PluginArea.Client
            ? await _frameOrigins.ResolveAsync(manifest, held.Value, ct)
            : [];

        return Result<PluginPageViewDto>.Success(new PluginPageViewDto(
            manifest.Id,
            page.Id,
            page.Path,
            texts.Resolve(page.Title),
            view,
            PluginFrameOriginResolver.LinkOrigins(manifest, frameOrigins),
            page.Layout ?? PluginPageLayouts.Standard,
            frameOrigins));
    }
}
