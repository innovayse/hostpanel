using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Queries.GetOrder;

/// <summary>
/// Loads a single order with all its items and maps it to <see cref="OrderDetailDto"/>.
/// Resolves the client display name and the linked invoice status and total from their own modules, and tells for each
/// line whether staff may retry it or close it by hand, with the same rule and lease the retry and the close use.
/// </summary>
public sealed class GetOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client's brief and the invoice's brief; Orders reads neither table.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The retry lease, the <c>Orders:RetryLeaseMinutes</c> option the retry itself uses.</summary>
    private readonly IOptions<OrderOptions> _options;

    /// <summary>The clock a line's lease is measured against.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client's brief and the invoice's brief.</param>
    /// <param name="options">The retry lease, the <c>Orders:RetryLeaseMinutes</c> option the retry itself uses.</param>
    /// <param name="clock">The clock a line's lease is measured against.</param>
    public GetOrderHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<OrderOptions> options, IClock clock)
    {
        _context = context;
        _bus = bus;
        _options = options;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The order to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order's detail, or <c>ORDER_NOT_FOUND</c> when the id names no order.</returns>
    public async Task<Result<OrderDetailDto>> HandleAsync(GetOrderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == query.OrderId)
            .Select(o => new
            {
                Dto = new OrderDetailDto(
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
                            item.LastFulfilmentAttemptAt,
                            false,
                            false))
                        .ToList(),
                    o.FraudScore,
                    Array.Empty<FraudReasonCode>(),
                    o.CreatedAt),
                o.FraudReasonCodes,
            })
            .FirstOrDefaultAsync(ct);
        if (found is null)
        {
            return Result<OrderDetailDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        var dto = found.Dto with { FraudReasons = FraudReasonText.Parse(found.FraudReasonCodes) };

        // FK_Orders_Clients_ClientId restricts deleting a client that has orders, so a missing client
        // is a broken database, not an answer to render. A missing invoice reads as null.
        var client = await _bus.GetRequiredClientBriefAsync(dto.ClientId, withEmail: false, ct);
        var invoice = dto.InvoiceId is { } invoiceId
            ? await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct)
            : null;

        // Decided here with the rule the retry and the close apply, so the buttons the admin shows are the calls it may make.
        var now = _clock.UtcNow;
        var lease = _options.Value.RetryLease;
        var items = dto.Items
            .Select(item => item with
            {
                CanRetry = Order.IsLineRetryable(dto.Status, item.FulfilmentState, item.LastFulfilmentAttemptAt, now, lease),
                CanClose = OrderItem.IsClosableByHand(item.FulfilmentState, item.LastFulfilmentAttemptAt, now, lease),
            })
            .ToList();

        return Result.Success(dto with
        {
            Items = items,
            ClientName = client.FullName,
            InvoiceStatus = invoice is { IsSuccess: true } ? invoice.Value!.Status : null,
            InvoiceTotal = invoice is { IsSuccess: true } ? invoice.Value!.Total : null,
        });
    }
}
