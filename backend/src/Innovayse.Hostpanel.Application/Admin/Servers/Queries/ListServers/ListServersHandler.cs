using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
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

        // The revenue is read for the servers of the page only, by the query the revenue forecast report reads too.
        var rates = await _context.GetBaseCurrencyRatesAsync(ct);
        var revenue = await _context.GetServerRevenueAsync(rates, servers.Select(s => s.Id).ToList(), ct);

        return Result.Success(new PaginatedList<ServerDto>(
            servers.Select(s => ServerMapper.ToDto(s, groupMap, UsageOf(revenue, s.Id, rates))).ToList(), totalCount, query.Page, query.PageSize));
    }

    /// <summary>Reads what one server's active services renew for, rounded to the base currency's minor unit.</summary>
    /// <param name="revenue">The revenue of the page's servers that have an active service, by server id.</param>
    /// <param name="serverId">The server.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <returns>The server's usage; zero services and zero revenue when it has no active service.</returns>
    private static ServerUsageDto UsageOf(IReadOnlyDictionary<int, ServerRevenue> revenue, int serverId, BaseCurrencyRates rates)
    {
        var served = revenue.GetValueOrDefault(serverId, new ServerRevenue(0, 0m));
        return new ServerUsageDto(served.ActiveServices, served.Monthly.RoundedMonthly(rates), rates.Currency);
    }
}
