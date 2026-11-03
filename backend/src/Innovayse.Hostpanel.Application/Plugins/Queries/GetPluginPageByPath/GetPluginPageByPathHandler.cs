using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPageByPath;

/// <summary>
/// Handles <see cref="GetPluginPageByPathQuery"/>, so a panel can open a plugin page from its own address (a reload or
/// a shared link) without first reading the index. A path of the wrong shape is refused first
/// (<see cref="PluginPagePathMatcher.Segments"/>); then <see cref="PluginPageReader"/> answers exactly what the lookup by
/// id answers for the page the path names (<see cref="PluginPagePathMatcher.Match"/>): the same refusals in the same
/// order, every one of them the same <c>PLUGIN_PAGE_NOT_FOUND</c> except the identity plugin's outage.
/// </summary>
public sealed class GetPluginPageByPathHandler
{
    /// <summary>Reads the page with the refusal order every page lookup shares.</summary>
    private readonly PluginPageReader _reader;

    /// <summary>Initializes the handler.</summary>
    /// <param name="source">The plugins whose pages the panels may show.</param>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    public GetPluginPageByPathHandler(IPluginUiSource source, ICallerClientResolver callerClient)
    {
        _reader = new PluginPageReader(source, callerClient);
    }

    /// <summary>Finds, reads and localises the page the path names.</summary>
    /// <param name="query">The area, the plugin id and the page path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The page; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who a client caller is;
    /// <c>PLUGIN_PAGE_NOT_FOUND</c> for every other refusal: a missing or malformed path, a plugin id that is not well
    /// formed, a plugin that is not available or declares no page at that path in the area, or a client caller who acts
    /// for no account or lacks the page's permission.
    /// </returns>
    public async Task<Result<PluginPageViewDto>> HandleAsync(GetPluginPageByPathQuery query, CancellationToken ct)
    {
        // A path no page could have is refused before anything is looked up, with the very answer an unknown page gets:
        // a 400 would tell a caller which paths are well formed.
        if (PluginPagePathMatcher.Segments(query.Path) is not { } segments)
        {
            return Result<PluginPageViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }

        return await _reader.ReadAsync(
            query.Area, query.PluginId, pages => PluginPagePathMatcher.Match(pages, segments), ct);
    }
}
