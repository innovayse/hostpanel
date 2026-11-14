using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Extensions;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;

/// <summary>
/// Returns a page of the fraud log, narrowed by the search text on the e-mail and IP address and by kind, decision and
/// reason, all in SQL ahead of paging.
/// </summary>
public sealed class ListFraudLogHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListFraudLogHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page, page size, search and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of log entries. Never refuses.</returns>
    public async Task<Result<PaginatedList<FraudLogEntryDto>>> HandleAsync(ListFraudLogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.FraudLogEntries.AsNoTracking().Search(query.Search, l => l.Email, l => l.IpAddress);

        if (query.Kind is { } kind)
        {
            filtered = filtered.Where(l => l.Kind == kind);
        }

        if (query.Decision is { } decision)
        {
            filtered = filtered.Where(l => l.Decision == decision);
        }

        if (query.Reason is { } reason)
        {
            // The stored text holds each reason between two commas, so the marker never matches part of another name.
            var marker = FraudReasonText.Marker(reason);
            filtered = filtered.Where(l => l.ReasonCodes.Contains(marker));
        }

        var ordered = filtered.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id);
        var totalCount = await ordered.CountAsync(ct);
        var entries = await ordered.Paginate(query.Page, query.PageSize).ToListAsync(ct);

        return Result.Success(new PaginatedList<FraudLogEntryDto>(entries.Select(e => e.ToDto()).ToList(), totalCount, query.Page, query.PageSize));
    }
}
