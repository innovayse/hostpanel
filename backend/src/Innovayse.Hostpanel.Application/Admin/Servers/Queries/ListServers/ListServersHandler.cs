using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;

/// <summary>
/// Loads one page of servers, ordered by name, narrowed by the search text, the module and the last known state in SQL
/// ahead of paging, and looks up the group names of the page's servers only.
/// </summary>
public sealed class ListServersHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListServersHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size, search, module and state filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of servers mapped to <see cref="ServerDto"/>, by name. Never refuses.</returns>
    public async Task<Result<PaginatedList<ServerDto>>> HandleAsync(ListServersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.Servers.AsNoTracking()
            .Search(query.Search, s => s.Name, s => s.Hostname, s => s.IpAddress)
            .Matching(query.Module, query.State);

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = filtered.OrderBy(s => s.Name).ThenBy(s => s.Id);
        var totalCount = await ordered.CountAsync(ct);
        var servers = await ordered.Paginate(query.Page, query.PageSize).ToListAsync(ct);

        var groupIds = servers.Select(s => s.ServerGroupId).OfType<int>().Distinct().ToList();
        var groupMap = await _context.ServerGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        return Result.Success(new PaginatedList<ServerDto>(
            servers.Select(s => ServerMapper.ToDto(s, groupMap)).ToList(), totalCount, query.Page, query.PageSize));
    }
}
