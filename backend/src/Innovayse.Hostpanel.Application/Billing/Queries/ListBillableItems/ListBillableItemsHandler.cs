using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListBillableItems;

/// <summary>
/// Returns a paginated list of billable items with client names, narrowed by the search text, the client, the type and
/// whether the item is invoiced in SQL ahead of paging.
/// </summary>
public sealed class ListBillableItemsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs; Billing never reads the client tables.</param>
    public ListBillableItemsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of billable item DTOs. Never refuses.</returns>
    public async Task<Result<PaginatedList<BillableItemDto>>> HandleAsync(
        ListBillableItemsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientsMatchingSearch = await _bus.FindClientIdsMatchingAsync(query.Search, query.ClientId, ct);
        var filtered = _context.BillableItems.AsNoTracking().Matching(query, clientsMatchingSearch);
        if (query.ClientId is { } clientId)
        {
            filtered = filtered.Where(x => x.ClientId == clientId);
        }

        if (query.Invoiced is { } invoiced)
        {
            filtered = filtered.Where(x => x.IsInvoiced == invoiced);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = filtered.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Paginate(query.Page, query.PageSize)
            .Select(item => new BillableItemDto(
                item.Id, item.ClientId, string.Empty, item.Description, item.Amount, item.Currency,
                item.Type, item.RecurringPeriod, item.IsInvoiced, item.InvoiceId, item.NextDueDate,
                item.CreatedAt))
            .ToListAsync(ct);

        var clientIds = items.Select(i => i.ClientId).Distinct().ToList();
        var clientMap = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items
            .Select(i => i with { ClientName = clientMap[i.ClientId].FullName })
            .ToList();

        return Result<PaginatedList<BillableItemDto>>.Success(
            new PaginatedList<BillableItemDto>(dtos, total, query.Page, query.PageSize));
    }
}
