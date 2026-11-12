using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Extensions;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListBlockListEntries;

/// <summary>Returns a page of the block list, optionally for one type.</summary>
public sealed class ListBlockListEntriesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListBlockListEntriesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size and type filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of entries. Never refuses.</returns>
    public async Task<Result<PaginatedList<BlockListEntryDto>>> HandleAsync(ListBlockListEntriesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        IQueryable<BlockListEntry> filtered = _context.BlockListEntries.AsNoTracking();
        if (query.Type is { } type)
        {
            filtered = filtered.Where(e => e.Type == type);
        }

        var ordered = filtered.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id);
        var totalCount = await ordered.CountAsync(ct);
        var entries = await ordered.Paginate(page, pageSize).ToListAsync(ct);

        return Result.Success(new PaginatedList<BlockListEntryDto>(entries.Select(e => e.ToDto()).ToList(), totalCount, page, pageSize));
    }
}
