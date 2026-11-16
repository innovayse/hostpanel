using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListNetworkIssues;

/// <summary>Returns a page of network issues, narrowed by the search text and the status, type and priority filters, newest first.</summary>
public sealed class ListNetworkIssuesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListNetworkIssuesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the page. This use case answers no refusal code.</returns>
    public async Task<Result<PaginatedList<NetworkIssueDto>>> HandleAsync(ListNetworkIssuesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var issues = _context.NetworkIssues
            .AsNoTracking()
            .Search(query.Search, n => n.Title, n => n.Server, n => n.Description);

        if (query.Status is { } status)
        {
            issues = issues.Where(n => n.Status == status);
        }

        if (query.Type is { } type)
        {
            issues = issues.Where(n => n.Type == type);
        }

        if (query.Priority is { } priority)
        {
            issues = issues.Where(n => n.Priority == priority);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = issues.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);
        var totalCount = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .SelectDto()
            .ToListAsync(ct);

        return Result.Success(new PaginatedList<NetworkIssueDto>(items, totalCount, query.Page, query.PageSize));
    }
}
