using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPage;

/// <summary>
/// Handles <see cref="GetPluginPageQuery"/>. A page id that is not <see cref="PluginPageIdentifier.IsValid"/> is refused
/// first; then <see cref="PluginPageReader"/> does the rest, the steps the lookup by path shares: every refusal of a
/// page answers the same <c>PLUGIN_PAGE_NOT_FOUND</c> (an unknown plugin, a disabled one, one that is not a serving plugin,
/// a page it does not declare, a caller who acts for no account and a caller who lacks the page's permission are
/// indistinguishable), so the endpoint is no oracle for which plugins and pages exist. The one other answer is the
/// identity plugin's outage.
/// </summary>
public sealed class GetPluginPageHandler
{
    /// <summary>Reads the page with the refusal order every page lookup shares.</summary>
    private readonly PluginPageReader _reader;

    /// <summary>Initializes the handler.</summary>
    /// <param name="source">The plugins whose pages the panels may show.</param>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    /// <param name="facts">Reads the domains of the caller's own hosting services.</param>
    /// <param name="slots">Reads the products the operator bound a plugin's slot widgets to.</param>
    /// <param name="preview">The origins allowed over plain http on a developer machine.</param>
    /// <param name="portal">The panel's own address, whose origin is never framed.</param>
    public GetPluginPageHandler(
        IPluginUiSource source,
        ICallerClientResolver callerClient,
        IPluginHostFactsReader facts,
        IPluginSlotContextReader slots,
        IOptions<PluginPreviewOptions> preview,
        IOptions<ClientPortalOptions> portal)
    {
        _reader = new PluginPageReader(source, callerClient, facts, slots, preview, portal);
    }

    /// <summary>Reads and localises the page.</summary>
    /// <param name="query">The area, the plugin id and the page id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The page; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who a client caller is;
    /// <c>PLUGIN_PAGE_NOT_FOUND</c> for every other refusal: an id that is not well formed, a plugin that is not available
    /// or does not declare the page in the area, or a client caller who acts for no account or lacks the page's permission.
    /// </returns>
    public async Task<Result<PluginPageViewDto>> HandleAsync(GetPluginPageQuery query, CancellationToken ct)
    {
        // A page id that no manifest could declare is refused before anything is looked up, with the very answer an
        // unknown page gets: a 400 would tell a caller which ids are well formed.
        if (!PluginPageIdentifier.IsValid(query.PageId))
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }

        return await _reader.ReadAsync(
            query.Area,
            query.PluginId,
            pages => pages.FirstOrDefault(
                candidate => string.Equals(candidate.Id, query.PageId, StringComparison.Ordinal)),
            ct);
    }
}
