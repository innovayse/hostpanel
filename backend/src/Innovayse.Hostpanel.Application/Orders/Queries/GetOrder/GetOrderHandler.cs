using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Queries.GetOrder;

/// <summary>
/// Loads a single order with all its items and maps it to <see cref="OrderDetailDto"/>.
/// Resolves the client display name and the linked invoice status and total from their own modules.
/// </summary>
public sealed class GetOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client's brief and the invoice's brief; Orders reads neither table.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client's brief and the invoice's brief.</param>
    public GetOrderHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The order to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order's detail, or <c>ORDER_NOT_FOUND</c> when the id names no order.</returns>
    public async Task<Result<OrderDetailDto>> HandleAsync(GetOrderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dto = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == query.OrderId)
            .Select(o => new OrderDetailDto(
                o.Id,
                o.OrderNumber,
                o.ClientId,
                string.Empty,
                o.Status,
                o.PaymentMethod,
                o.Items.Sum(item => item.FirstPaymentAmount),
                o.Currency,
                o.InvoiceId,
                null,
                null,
                o.IpAddress,
                o.Notes,
                o.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemDto(
                        item.Id,
                        item.ProductId,
                        item.ProductName,
                        item.BillingCycle,
                        item.Domain,
                        item.Hostname,
                        item.FirstPaymentAmount,
                        item.RecurringAmount,
                        item.Status,
                        item.FulfilmentState,
                        item.FulfilmentErrorCode,
                        item.FulfilmentAttempts,
                        item.LastFulfilmentAttemptAt))
                    .ToList(),
                o.CreatedAt))
            .FirstOrDefaultAsync(ct);
        if (dto is null)
        {
            return Result<OrderDetailDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        // FK_Orders_Clients_ClientId restricts deleting a client that has orders, so a missing client
        // is a broken database, not an answer to render. A missing invoice reads as null.
        var client = await _bus.GetRequiredClientBriefAsync(dto.ClientId, withEmail: false, ct);
        var invoice = dto.InvoiceId is { } invoiceId
            ? await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct)
            : null;

        return Result.Success(dto with
        {
            ClientName = client.FullName,
            InvoiceStatus = invoice is { IsSuccess: true } ? invoice.Value!.Status : null,
            InvoiceTotal = invoice is { IsSuccess: true } ? invoice.Value!.Total : null,
        });
    }
}
