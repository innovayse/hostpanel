using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Queries.ListOrders;

/// <summary>
/// Returns a paged list of orders, narrowed by the search text on the order number and id and by status in SQL ahead of
/// paging, mapped to <see cref="OrderListItemDto"/>. Batch-fetches client names from the Clients module in one dispatch to avoid N+1 queries.
/// </summary>
public sealed class ListOrdersHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client briefs; Orders never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client briefs.</param>
    public ListOrdersHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the status filter and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of order summaries. Never refuses.</returns>
    public async Task<Result<PaginatedList<OrderListItemDto>>> HandleAsync(ListOrdersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.Orders.AsNoTracking().SearchIncludingId(query.Search, o => o.Id, o => o.OrderNumber);
        if (query.Status is { } status)
        {
            filtered = filtered.Where(o => o.Status == status);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = filtered.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);
        var totalCount = await ordered.CountAsync(ct);
        var orders = await ordered
            .Paginate(query.Page, query.PageSize)
            .Include(o => o.Items)
            .ToListAsync(ct);

        var clientIds = orders.Select(o => o.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var items = orders
            .Select(o => new OrderListItemDto(
                o.Id,
                o.OrderNumber,
                o.ClientId,
                clients[o.ClientId].FullName,
                o.Status,
                o.PaymentMethod,
                o.Items.Sum(i => i.FirstPaymentAmount),
                o.Currency,
                o.InvoiceId,
                o.Items.Count,
                o.Items.Count(i => i.FulfilmentState == OrderItemFulfilmentState.Failed || i.FulfilmentState == OrderItemFulfilmentState.PartiallyRefunded),
                o.IpAddress,
                o.FraudScore,
                o.FraudReasons,
                o.CreatedAt))
            .ToList();

        return Result.Success(new PaginatedList<OrderListItemDto>(items, totalCount, query.Page, query.PageSize));
    }
}
