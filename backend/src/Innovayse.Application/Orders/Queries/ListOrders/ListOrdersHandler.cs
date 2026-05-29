namespace Innovayse.Application.Orders.Queries.ListOrders;

using Innovayse.Application.Common;
using Innovayse.Application.Orders.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;

/// <summary>
/// Returns a paged list of all orders mapped to <see cref="OrderListItemDto"/>.
/// Batch-fetches client names to avoid N+1 queries.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="clientRepo">Client repository for batch client-name lookups.</param>
public sealed class ListOrdersHandler(IOrderRepository orderRepo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="ListOrdersQuery"/>.
    /// </summary>
    /// <param name="query">The list orders query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing order summary DTOs.</returns>
    public async Task<PagedResult<OrderListItemDto>> HandleAsync(ListOrdersQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var statusFilter = ParseStatus(query.Status);

        var (orders, totalCount) = await orderRepo.ListAsync(page, pageSize, statusFilter, ct);

        var clientIds = orders.Select(o => o.ClientId).Distinct().ToList();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        var items = orders
            .Select(o => new OrderListItemDto(
                o.Id,
                o.OrderNumber,
                o.ClientId,
                clientMap.GetValueOrDefault(o.ClientId, "Unknown"),
                o.Status.ToString(),
                o.PaymentMethod,
                o.Items.Sum(i => i.FirstPaymentAmount),
                o.InvoiceId,
                o.Items.Count,
                o.CreatedAt))
            .ToList();

        return new PagedResult<OrderListItemDto>(items, totalCount, page, pageSize);
    }

    /// <summary>
    /// Parses a status filter string to <see cref="OrderStatus"/>.
    /// Returns <see langword="null"/> when the input is null, empty, or not a valid enum value.
    /// </summary>
    /// <param name="status">The status string to parse.</param>
    /// <returns>The parsed <see cref="OrderStatus"/>, or null if parsing fails.</returns>
    private static OrderStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;
    }
}
