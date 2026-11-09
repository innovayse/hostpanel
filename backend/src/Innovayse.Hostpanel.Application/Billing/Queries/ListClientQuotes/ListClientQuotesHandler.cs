using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;

/// <summary>Returns a paginated list of quotes for a specific client.</summary>
public sealed class ListClientQuotesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    public ListClientQuotesHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The list client quotes query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of quote list items for the client. Never refuses.</returns>
    public async Task<Result<PaginatedList<QuoteListItemDto>>> HandleAsync(
        ListClientQuotesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        var ordered = _context.Quotes
            .AsNoTracking()
            .Where(x => x.ClientId == query.ClientId)
            .OrderByDescending(x => x.CreatedAt);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(page, pageSize)
            .SelectDto()
            .ToListAsync(ct);

        var clientIds = items.Select(q => q.ClientId).Distinct().ToList();
        var clientMap = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items.Select(q => q with { ClientName = clientMap[q.ClientId].FullName }).ToList();

        return Result<PaginatedList<QuoteListItemDto>>.Success(
            new PaginatedList<QuoteListItemDto>(dtos, total, page, pageSize));
    }
}
