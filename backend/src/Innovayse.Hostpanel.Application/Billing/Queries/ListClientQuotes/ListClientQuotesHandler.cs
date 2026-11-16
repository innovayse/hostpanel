using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;

/// <summary>
/// Returns a paginated list of quotes for a specific client, narrowed by the search text, the stage and the validity in
/// SQL ahead of paging.
/// </summary>
public sealed class ListClientQuotesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock a quote's validity is judged against.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    /// <param name="clock">The clock a quote's validity is judged against.</param>
    public ListClientQuotesHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client, the search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of quote list items for the client. Never refuses.</returns>
    public async Task<Result<PaginatedList<QuoteListItemDto>>> HandleAsync(
        ListClientQuotesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = _context.Quotes
            .AsNoTracking()
            .Where(q => q.ClientId == query.ClientId)
            .Matching(query, _clock.UtcNow)
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .SelectDto()
            .ToListAsync(ct);

        var clientIds = items.Select(q => q.ClientId).Distinct().ToList();
        var clientMap = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items.Select(q => q with { ClientName = clientMap[q.ClientId].FullName }).ToList();

        return Result<PaginatedList<QuoteListItemDto>>.Success(
            new PaginatedList<QuoteListItemDto>(dtos, total, query.Page, query.PageSize));
    }
}
