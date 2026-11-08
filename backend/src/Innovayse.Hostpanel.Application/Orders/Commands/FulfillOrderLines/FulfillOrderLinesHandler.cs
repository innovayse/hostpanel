using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.LinkDomainService;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;
using Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;
using Innovayse.Hostpanel.Application.Services.Commands.OrderService;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;

/// <summary>
/// Handles <see cref="FulfillOrderLinesCommand"/>: runs every line of an accepted order that is not
/// fulfilled yet through the use case its type needs, never stopping at a failed line, records each
/// line's outcome on the line, and reports each line it could not fulfil so the caller can refund it
/// or show it.
/// </summary>
public sealed class FulfillOrderLinesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for the Domains and Services use cases each line needs.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<FulfillOrderLinesHandler> _logger;

    /// <summary>The clock that dates each line's attempt.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus, for the Domains and Services use cases each line needs.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock that dates each line's attempt.</param>
    public FulfillOrderLinesHandler(
        IHostpanelDbContext context, IMessageBus bus, ILogger<FulfillOrderLinesHandler> logger, IClock clock)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order whose lines are fulfilled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The lines that could not be fulfilled (empty when all were), <c>ORDER_NOT_FOUND</c> when the
    /// id names no order, or <c>ORDER_ITEM_NOT_FOUND</c> when a retry names a line the order does not hold.
    /// A failed line never stops the others, and a line already fulfilled (or refunded) is left as it is.
    /// </returns>
    public async Task<Result<OrderFulfillmentDto>> HandleAsync(FulfillOrderLinesCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (cmd.ItemId is { } onlyItemId && order.Items.All(i => i.Id != onlyItemId))
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        var digitalProductIds = await FindDigitalProductIdsAsync(order, ct);
        var failures = new List<OrderLineFailureDto>();
        int? createdDomainId = null;
        int? createdServiceId = null;
        string? orderDomainName = null;

        foreach (var item in order.Items.OrderBy(i => i.Id))
        {
            if (cmd.ItemId is not null && item.Id != cmd.ItemId)
            {
                continue;
            }

            // A line a retry is running belongs to that retry; a run over the whole order must not start it a second time.
            if (cmd.ItemId is null && item.FulfilmentState is OrderItemFulfilmentState.InProgress)
            {
                continue;
            }

            if (item.FulfilmentState is OrderItemFulfilmentState.Fulfilled or OrderItemFulfilmentState.Refunded)
            {
                // Done already (or given back): never fulfilled twice. A fulfilled domain line still
                // tells a service line of the same order which domain it was bought with.
                if (item.DomainAction is not null && item.Domain is not null && item.FulfilmentState is OrderItemFulfilmentState.Fulfilled)
                {
                    orderDomainName = item.Domain;
                }

                continue;
            }

            if (item.DomainAction is not null)
            {
                if (item.Domain is null)
                {
                    // A domain-action line always names a domain; one that does not is a data fault,
                    // logged loud and reported, with no domain to act on.
                    _logger.LogCritical(
                        "Order {OrderId} item {ItemId} has domain action '{Action}' but no domain name; " +
                        "the line was not fulfilled and needs manual intervention.",
                        order.Id, item.Id, item.DomainAction);
                    failures.Add(Failure(
                        item, OrderLineKind.Domain, null, $"Order {order.Id} item {item.Id} has a domain action but no domain name."));
                    order.RecordLineFailed(item.Id, null, _clock.UtcNow);
                    continue;
                }

                var domain = await FulfillDomainActionAsync(order, item, item.Domain, cmd.ItemId is not null, ct);
                if (domain.FailureReason is not null)
                {
                    failures.Add(Failure(item, OrderLineKind.Domain, domain.ErrorCode, domain.FailureReason));
                    order.RecordLineFailed(item.Id, domain.ErrorCode, _clock.UtcNow);
                }
                else
                {
                    createdDomainId ??= domain.DomainId;
                    orderDomainName = item.Domain;
                    order.RecordLineFulfilled(item.Id, _clock.UtcNow);
                }

                continue;
            }

            var service = await FulfillServiceAsync(
                order, item, item.Domain ?? orderDomainName, digitalProductIds.Contains(item.ProductId), ct);
            if (service.IsFailure)
            {
                failures.Add(Failure(item, OrderLineKind.Service, service.ErrorCode, DescribeFailure(service)));
                order.RecordLineFailed(item.Id, service.ErrorCode, _clock.UtcNow);
            }
            else
            {
                createdServiceId = service.Value;
                order.RecordLineFulfilled(item.Id, _clock.UtcNow);
            }
        }

        // The outcomes are saved before the link below, so the record of what happened never waits on a convenience.
        await _context.SaveChangesAsync(ct);

        // Auto-link a domain to a hosting service bought in the same order. The link is a
        // convenience: a refusal is logged and never turns a fulfilled order into a failure.
        if (createdDomainId.HasValue && createdServiceId.HasValue)
        {
            var linked = await _bus.InvokeAsync<Result>(
                new LinkDomainServiceCommand(createdDomainId.Value, createdServiceId.Value), ct);
            if (linked.IsFailure)
            {
                _logger.LogWarning(
                    "Order {OrderId}: domain {DomainId} could not be linked to service {ServiceId}: {Reason}.",
                    order.Id, createdDomainId.Value, createdServiceId.Value, DescribeFailure(linked));
            }
        }

        return Result.Success(new OrderFulfillmentDto(failures));
    }

    /// <summary>
    /// Creates the client service a product line bought. A refusal or a fault is logged loud and
    /// returned, so one failing line never aborts the rest.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The product line.</param>
    /// <param name="domain">The domain the service is for: the line's own, else one ordered beside it.</param>
    /// <param name="isDigital">
    /// Whether the line's product is a digital one. Such a service has no hosting account to set up, so it is
    /// activated at once, without provisioning; any other service stays Pending until the client sets it up.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new service's id, or the failure the line ended with.</returns>
    private async Task<Result<int>> FulfillServiceAsync(Order order, OrderItem item, string? domain, bool isDigital, CancellationToken ct)
    {
        Result<int> outcome;
        try
        {
            outcome = await _bus.InvokeAsync<Result<int>>(
                new OrderServiceCommand(
                    order.ClientId, item.ProductId, item.BillingCycle,
                    item.FirstPaymentAmount, item.RecurringAmount,
                    order.PaymentMethod, domain), ct);
        }
        catch (Exception ex)
        {
            // A genuine fault out of the dispatch, not an answer of the use case.
            _logger.LogCritical(
                ex,
                "Service provisioning failed for order {OrderId} item {ItemId} (product {ProductId}); " +
                "the order is accepted but this line was never provisioned and needs manual intervention.",
                order.Id, item.Id, item.ProductId);
            return Result<int>.FailureCode(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        if (outcome.IsFailure)
        {
            _logger.LogCritical(
                "Service provisioning failed for order {OrderId} item {ItemId} (product {ProductId}): {Reason}. " +
                "The order is accepted but this line was never provisioned and needs manual intervention.",
                order.Id, item.Id, item.ProductId, DescribeFailure(outcome));
            return outcome;
        }

        return isDigital ? await ActivateDigitalAsync(order, item, outcome.Value, ct) : outcome;
    }

    /// <summary>
    /// Activates the service of a digital line, which has no hosting account to provision. A refusal or a
    /// fault is logged loud and returned, so one failing line never aborts the rest; the service is then left Pending for staff.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The digital product line.</param>
    /// <param name="serviceId">The service the line created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service's id once it is Active, or the failure the line ended with.</returns>
    private async Task<Result<int>> ActivateDigitalAsync(Order order, OrderItem item, int serviceId, CancellationToken ct)
    {
        Result activated;
        try
        {
            activated = await _bus.InvokeAsync<Result>(new ActivateDigitalServiceCommand(serviceId), ct);
        }
        catch (Exception ex)
        {
            // A genuine fault out of the dispatch, not an answer of the use case.
            _logger.LogCritical(
                ex,
                "Activation of digital service {ServiceId} failed for order {OrderId} item {ItemId} (product {ProductId}); " +
                "the service stays Pending and needs manual intervention.",
                serviceId, order.Id, item.Id, item.ProductId);
            return Result<int>.FailureCode(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        if (activated.IsFailure)
        {
            _logger.LogCritical(
                "Activation of digital service {ServiceId} failed for order {OrderId} item {ItemId} (product {ProductId}): {Reason}. " +
                "The service stays Pending and needs manual intervention.",
                serviceId, order.Id, item.Id, item.ProductId, DescribeFailure(activated));
            return Result<int>.FailureCode(activated.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        return Result<int>.Success(serviceId);
    }

    /// <summary>Finds which of the order's service lines name a digital product.</summary>
    /// <param name="order">The accepted order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the digital products among the lines' products; a product the Products module does not know is not digital.</returns>
    private async Task<IReadOnlySet<int>> FindDigitalProductIdsAsync(Order order, CancellationToken ct)
    {
        var productIds = order.Items.Where(i => i.DomainAction is null).Select(i => i.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(new GetProductsByIdsQuery(productIds), ct);
        return (products.Value ?? []).Where(p => p.Type == ProductType.Digital).Select(p => p.Id).ToHashSet();
    }

    /// <summary>Runs the one domain action an order line carries.</summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The line item, already known to carry a domain action.</param>
    /// <param name="domain">The domain name from the order line.</param>
    /// <param name="isRetry">
    /// Whether the line is being retried. A retried registration or transfer finds the domain this client
    /// already holds under that name and counts it as done, so a first attempt that did register it (and
    /// failed afterwards) is never repeated.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new domain's id when this action creates one (register, transfer) and it succeeded;
    /// otherwise no id. The failure reason is <see langword="null"/> on success and a
    /// human-readable sentence on failure.
    /// </returns>
    private async Task<DomainActionOutcome> FulfillDomainActionAsync(
        Order order, OrderItem item, string domain, bool isRetry, CancellationToken ct)
    {
        if (isRetry && item.DomainAction is DomainAction.Register or DomainAction.Transfer)
        {
            if (await IsDomainHeldAsync(order, domain, ct))
            {
                _logger.LogInformation(
                    "Order {OrderId} item {ItemId}: domain '{Domain}' is already held by client {ClientId}; the retry counts it as done.",
                    order.Id, item.Id, domain, order.ClientId);
                return new DomainActionOutcome(null, null, null);
            }
        }

        if (item.DomainAction == DomainAction.Register)
        {
            var outcome = await _bus.InvokeAsync<Result<int>>(
                new RegisterDomainCommand(
                    order.ClientId,
                    domain,
                    item.Years ?? 1,
                    WhoisPrivacy: false,
                    AutoRenew: true,
                    Nameserver1: null,
                    Nameserver2: null,
                    FirstPaymentAmount: item.FirstPaymentAmount,
                    RecurringAmount: item.RecurringAmount,
                    PaymentMethod: order.PaymentMethod,
                    PriceCurrency: order.Currency), ct);

            return outcome.IsFailure
                ? new DomainActionOutcome(null, outcome.ErrorCode, DescribeFailure(outcome))
                : new DomainActionOutcome(outcome.Value, null, null);
        }

        if (item.DomainAction == DomainAction.Transfer)
        {
            if (item.EppCode is null)
            {
                return new DomainActionOutcome(
                    null, null, $"Order {order.Id} item {item.Id} is a transfer for '{domain}' but has no EPP code.");
            }

            var outcome = await _bus.InvokeAsync<Result<int>>(
                new TransferDomainCommand(
                    order.ClientId,
                    domain,
                    item.EppCode,
                    WhoisPrivacy: false,
                    FirstPaymentAmount: item.FirstPaymentAmount,
                    RecurringAmount: item.RecurringAmount,
                    PaymentMethod: order.PaymentMethod,
                    Years: item.Years ?? 1), ct);

            return outcome.IsFailure
                ? new DomainActionOutcome(null, outcome.ErrorCode, DescribeFailure(outcome))
                : new DomainActionOutcome(outcome.Value, null, null);
        }

        if (item.DomainAction == DomainAction.Renew)
        {
            // Extends a domain the client already owns; RenewOrderedDomainCommand refuses a domain
            // that does not exist or is not this client's.
            var renewed = await _bus.InvokeAsync<Result>(
                new RenewOrderedDomainCommand(order.ClientId, domain, item.Years ?? 1), ct);
            return renewed.IsFailure
                ? new DomainActionOutcome(
                    null, renewed.ErrorCode, $"Order {order.Id} could not renew '{domain}': {DescribeFailure(renewed)}")
                : new DomainActionOutcome(null, null, null);
        }

        // No action this handler recognises: the line is accepted as-is, and the domain name is
        // still recorded for the auto-link check.
        return new DomainActionOutcome(null, null, null);
    }

    /// <summary>
    /// Asks the Domains module whether the order's client already holds a domain under the line's name, which is
    /// what a first attempt that did register it (and died before recording the line) leaves behind.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="domain">The domain name from the order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the client holds the domain.</returns>
    private async Task<bool> IsDomainHeldAsync(Order order, string domain, CancellationToken ct)
    {
        var owned = await _bus.InvokeAsync<Result<IReadOnlyList<string>>>(
            new GetOwnedDomainNamesQuery(order.ClientId, [domain]), ct);
        return owned.IsSuccess && owned.Value!.Count > 0;
    }

    /// <summary>Builds the report of one line that could not be fulfilled.</summary>
    /// <param name="item">The failed line.</param>
    /// <param name="kind">Whether it is a service or a domain line.</param>
    /// <param name="errorCode">The refusing use case's code, if any.</param>
    /// <param name="reason">The human-readable reason.</param>
    /// <returns>The line's failure report.</returns>
    private static OrderLineFailureDto Failure(OrderItem item, OrderLineKind kind, string? errorCode, string reason) =>
        new(item.Id, kind, item.ProductName, item.Domain, item.FirstPaymentAmount, errorCode, reason);

    /// <summary>Renders a failed <see cref="Result{T}"/> as one sentence for a log line or a refund note.</summary>
    /// <typeparam name="T">The value type of a successful result.</typeparam>
    /// <param name="result">The failed outcome to describe.</param>
    /// <returns>The result's message, its code, or a generic sentence, whichever is present first.</returns>
    private static string DescribeFailure<T>(Result<T> result) =>
        result.Error ?? result.ErrorCode ?? "The operation failed.";

    /// <summary>Renders a failed <see cref="Result"/> as one sentence for a log line or a refund note.</summary>
    /// <param name="result">The failed outcome to describe.</param>
    /// <returns>The result's message, its code, or a generic sentence, whichever is present first.</returns>
    private static string DescribeFailure(Result result) =>
        result.Error ?? result.ErrorCode ?? "The operation failed.";
}
