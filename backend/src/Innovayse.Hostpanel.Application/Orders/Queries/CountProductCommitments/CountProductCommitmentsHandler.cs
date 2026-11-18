using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Queries.CountLiveServices;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;

/// <summary>
/// Handles <see cref="CountProductCommitmentsQuery"/>: open order lines not yet fulfilled, read here, plus the live
/// services the Services module counts, leaving out a service created from one of those lines so it is counted once.
/// </summary>
public sealed class CountProductCommitmentsHandler
{
    /// <summary>Order states that will never become a service.</summary>
    private static readonly OrderStatus[] ClosedOrderStatuses = [OrderStatus.Cancelled, OrderStatus.Fraud];

    /// <summary>Line fulfilment states still on their way to a service.</summary>
    private static readonly OrderItemFulfilmentState[] OpenFulfilmentStates =
        [OrderItemFulfilmentState.Pending, OrderItemFulfilmentState.InProgress];

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the Services module's count is asked on.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the Services module's count is asked on.</param>
    public CountProductCommitmentsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The products, and optionally the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per requested product, 0 when nothing is committed, or the Services count's failure.</returns>
    public async Task<Result<IReadOnlyDictionary<int, int>>> HandleAsync(CountProductCommitmentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var productIds = query.ProductIds.Distinct().ToList();
        if (productIds.Count == 0)
        {
            return Result<IReadOnlyDictionary<int, int>>.Success(new Dictionary<int, int>());
        }

        var openLines = from item in _context.OrderItems.AsNoTracking()
                        join order in _context.Orders.AsNoTracking() on item.OrderId equals order.Id
                        where productIds.Contains(item.ProductId)
                            && item.Status != OrderItemStatus.Cancelled
                            && OpenFulfilmentStates.Contains(item.FulfilmentState)
                            && !ClosedOrderStatuses.Contains(order.Status)
                        select new { item.Id, item.ProductId, order.ClientId };

        if (query.ClientId is { } clientId)
        {
            openLines = openLines.Where(l => l.ClientId == clientId);
        }

        var lines = await openLines.Select(l => new { l.Id, l.ProductId }).ToListAsync(ct);

        var services = await _bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
            new CountLiveServicesQuery(productIds, query.ClientId, lines.Select(l => l.Id).ToList()), ct);
        if (services.IsFailure)
        {
            return Result<IReadOnlyDictionary<int, int>>.FailureCode(services.ErrorCode!);
        }

        IReadOnlyDictionary<int, int> answer = productIds.ToDictionary(
            id => id,
            id => services.Value!.GetValueOrDefault(id) + lines.Count(l => l.ProductId == id));
        return Result<IReadOnlyDictionary<int, int>>.Success(answer);
    }
}
