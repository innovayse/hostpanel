using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.LinkDomainService;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;
using Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;
using Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;
using Innovayse.Hostpanel.Application.Orders.Commands.StartOrderLineAction;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Application.Orders.Extensions;
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
/// Handles <see cref="FulfillOrderLinesCommand"/>: claims every line of an accepted order that is not
/// fulfilled yet, runs each through the use case its type needs, never stopping at a failed line, records each
/// line's outcome on the line the moment it is known, and reports each line it could not fulfil so the caller
/// can refund it or show it.
/// </summary>
/// <remarks>
/// The lines are claimed first (<see cref="ClaimOrderLinesCommand"/>: in progress, the attempt counted), so a run that dies
/// leaves them claimed and the order Incomplete, and a retry takes them over once the claim runs out. The run executes
/// exactly the lines its claim returned (or the sender handed over as <see cref="FulfillOrderLinesCommand.Claimed"/>), each
/// under the attempt number it was given there: a line another run's claim holds is never run by this one. A line's outcome is
/// recorded by <see cref="RecordOrderLineOutcomeCommand"/> in its own scope, one line at a time, together with the attempt
/// number the claim gave it, so the lines that finished are on record even when a later line throws, an outcome that
/// cannot be recorded never stops the other lines, a late outcome of an attempt whose claim was taken over is refused, and
/// the order's status is computed from the lines as the database holds them rather than from the copy this run loaded. A
/// line whose action throws is recorded as failed with a code and the run goes on; a cancelled run releases every line it
/// had not finished before it rethrows.
/// <para>
/// Right before each line's action the claim is checked and the line stamped (<see cref="StartOrderLineActionCommand"/>):
/// every line was claimed at once, so a line waiting its turn could have been taken over by a staff retry once its lease
/// ran out. Such a line is skipped, never run a second time, and the stamp starts the lease again for the line that runs.
/// </para>
/// </remarks>
public sealed class FulfillOrderLinesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for the Domains and Services use cases each line needs.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<FulfillOrderLinesHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus, for the Domains and Services use cases each line needs.</param>
    /// <param name="logger">Structured logger.</param>
    public FulfillOrderLinesHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<FulfillOrderLinesHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order whose lines are fulfilled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The lines that could not be fulfilled (empty when all were), <c>ORDER_NOT_FOUND</c> when the
    /// id names no order, <c>ORDER_ITEM_NOT_FOUND</c> when a retry names a line the order does not hold, or
    /// <c>ORDER_LINE_CHANGED</c> when the lines could not be claimed because the order kept changing, or when the one line
    /// a retry ran changed under it (a refund or a newer attempt overtook it) and its outcome could not be recorded. In a
    /// run over the whole order an outcome that cannot be recorded is logged and leaves its line claimed, retryable once
    /// the claim runs out, and the run still reports the other lines.
    /// A failed line never stops the others, and a line already fulfilled (or refunded) is left as it is.
    /// </returns>
    /// <exception cref="OperationCanceledException">The caller cancelled the run; the lines it had not finished are recorded as interrupted first.</exception>
    public async Task<Result<OrderFulfillmentDto>> HandleAsync(FulfillOrderLinesCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await LoadOrderAsync(cmd.OrderId, ct);
        if (order is null)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (cmd.ItemId is { } onlyItemId && order.Items.All(i => i.Id != onlyItemId))
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        try
        {
            // Claimed before any work starts, so a crash from here on leaves the lines claimed and the order Incomplete. A
            // sender that claimed the lines itself (acceptance, a retry) hands them over, and they are not claimed again.
            var claimed = cmd.Claimed;
            if (claimed is null)
            {
                var claim = await _bus.ClaimOrderLinesAsync(new ClaimOrderLinesCommand(order.Id, cmd.ItemId), ct);
                if (claim.IsFailure)
                {
                    return Result<OrderFulfillmentDto>.FailureCode(claim.ErrorCode ?? HostpanelErrorCodes.ORDER_LINE_CHANGED);
                }

                claimed = claim.Value!;
            }

            if (claimed.Count == 0)
            {
                _logger.LogInformation(
                    "Order {OrderId}: no line ran, because the claim took none: every line is done, given back, or held by another run.",
                    order.Id);
            }

            // Loaded again, read only: the lines as the claim left them, never the copy from before the claim.
            var claimedOrder = await LoadOrderAsync(order.Id, ct);
            if (claimedOrder is null)
            {
                _logger.LogWarning(
                    "Order {OrderId}: no line ran, because the order could not be read after {Count} line(s) were claimed; " +
                    "they stay claimed and can be retried when the claim runs out.",
                    order.Id, claimed.Count);
                return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
            }

            return await RunLinesAsync(claimedOrder, claimed, cmd.ItemId, ct);
        }
        finally
        {
            // Lines of one order can finish at the same moment, each run looking at the others as they were when it
            // began; the last to finish reads them all as they are now, so the order never stays Incomplete over
            // lines that are all done. Not cancellable: it must settle the status of lines already recorded.
            var refreshed = await _bus.RefreshOrderStatusAsync(order.Id, CancellationToken.None);
            if (refreshed.IsFailure)
            {
                _logger.LogWarning(
                    "Order {OrderId}: the status could not be brought in line with its lines: {Reason}.",
                    order.Id, DescribeFailure(refreshed));
            }
        }
    }

    /// <summary>Loads the order and its lines as the database holds them now; read only, nothing on it is saved.</summary>
    /// <param name="orderId">The order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order, or <see langword="null"/> when the id names none.</returns>
    private Task<Order?> LoadOrderAsync(int orderId, CancellationToken ct)
    {
        return _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    /// <summary>Runs the claimed lines in id order, each under the attempt its claim gave it, and records each outcome.</summary>
    /// <param name="order">The order as it stood after the claim; read only, nothing on it is saved.</param>
    /// <param name="claimed">The lines the claim gave this run, with their attempt numbers; no other line runs.</param>
    /// <param name="onlyItemId">The one line a retry runs, or <see langword="null"/> for a run over the whole order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lines that could not be fulfilled, or the refusal that stopped the run.</returns>
    private async Task<Result<OrderFulfillmentDto>> RunLinesAsync(
        Order order, IReadOnlyList<ClaimedOrderLineDto> claimed, int? onlyItemId, CancellationToken ct)
    {
        var lines = order.Items.OrderBy(i => i.Id).ToList();
        var attempts = claimed
            .Where(c => onlyItemId is null || c.ItemId == onlyItemId)
            .ToDictionary(c => c.ItemId, c => c.Attempt);
        var toRun = lines.Where(i => attempts.ContainsKey(i.Id)).ToList();
        var failures = new List<OrderLineFailureDto>();
        int? createdDomainId = null;
        int? createdServiceId = null;
        string? orderDomainName = null;
        string? recordRefusal = null;

        foreach (var item in lines)
        {
            if (!toRun.Contains(item))
            {
                // Done already (or given back): never fulfilled twice. A fulfilled domain line still
                // tells a service line of the same order which domain it was bought with.
                if (item.DomainAction is not null && item.Domain is not null && item.FulfilmentState is OrderItemFulfilmentState.Fulfilled)
                {
                    orderDomainName = item.Domain;
                }

                continue;
            }

            var attempt = attempts[item.Id];

            // Checked and stamped right before the action: a line a retry took over while it waited its turn is that retry's
            // to run, and must never be run twice.
            Result started;
            try
            {
                started = await StartActionAsync(order, item, attempt, ct);
            }
            catch (Exception ex) when (IsCancellation(ex, ct))
            {
                await ReleaseUnfinishedAsync(order, toRun.SkipWhile(i => i != item).ToList(), attempts);
                throw;
            }

            if (started.IsFailure)
            {
                _logger.LogWarning(
                    "Order {OrderId} item {ItemId}: attempt {Attempt} no longer holds the line ({Reason}); it is not run by this attempt.",
                    order.Id, item.Id, attempt, DescribeFailure(started));
                recordRefusal ??= started.ErrorCode ?? HostpanelErrorCodes.ORDER_LINE_CHANGED;
                continue;
            }

            OrderLineRunOutcome outcome;
            try
            {
                outcome = await RunLineAsync(order, item, attempt, orderDomainName, ct);
            }
            catch (Exception ex) when (IsCancellation(ex, ct))
            {
                await ReleaseUnfinishedAsync(order, toRun.SkipWhile(i => i != item).ToList(), attempts);
                throw;
            }
            catch (Exception ex)
            {
                // A genuine fault out of an action (a registrar that timed out, a dispatch that threw), not an answer
                // of a use case. Recorded as the line's failure so the lines before it stay on record and the lines
                // after it still run.
                _logger.LogCritical(
                    ex,
                    "Fulfilment of order {OrderId} item {ItemId} (product {ProductId}) threw; the order is accepted but this " +
                    "line was not fulfilled and needs a retry or manual intervention.",
                    order.Id, item.Id, item.ProductId);
                outcome = new OrderLineRunOutcome(
                    KindOf(item), null, null, HostpanelErrorCodes.HOSTPANEL_UNEXPECTED,
                    $"Order {order.Id} item {item.Id} could not be fulfilled: the action threw an unexpected error.");
            }

            // Recorded with a token that cannot be cancelled: the action has run, and what it did must reach the line.
            var recorded = await RecordOutcomeAsync(order, item, attempt, outcome);
            if (recorded.IsFailure)
            {
                // The other lines still have to run: this one stays claimed (or belongs to the attempt that took it over),
                // so a retry picks it up once the claim runs out, and what the attempt did is idempotent to repeat.
                _logger.LogCritical(
                    "Order {OrderId} item {ItemId}: the outcome of attempt {Attempt} could not be recorded: {Reason}. " +
                    "The line stays claimed and can be retried when the claim runs out.",
                    order.Id, item.Id, attempt, DescribeFailure(recorded));
                recordRefusal ??= recorded.ErrorCode ?? HostpanelErrorCodes.ORDER_LINE_CHANGED;
            }

            if (outcome.Failed)
            {
                // A failure that could not be recorded still names the attempt that holds the line, so the caller can
                // refund a refused domain all the same: the line is this run's, and it is not delivered.
                failures.Add(Failure(item, outcome, recorded.IsFailure ? attempt : null));
                continue;
            }

            createdDomainId ??= outcome.DomainId;
            createdServiceId = outcome.ServiceId ?? createdServiceId;
            if (item.DomainAction is not null)
            {
                orderDomainName = item.Domain;
            }
        }

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

        // A retry of one line reports a lost outcome as the refusal it is; a run over the whole order still reports the
        // lines it did, because the caller refunds the rejected ones from that report.
        return recordRefusal is not null && onlyItemId is not null
            ? Result<OrderFulfillmentDto>.FailureCode(recordRefusal)
            : Result.Success(new OrderFulfillmentDto(failures));
    }

    /// <summary>
    /// Records how the attempt ended on the line it claimed, under the attempt number its claim gave it. A fault while
    /// recording is answered as a failure to record, never thrown: the lines after this one must still run.
    /// </summary>
    /// <param name="order">The order the line belongs to.</param>
    /// <param name="item">The line, as it stood after the claim.</param>
    /// <param name="attempt">The attempt number the claim gave the line.</param>
    /// <param name="outcome">What running the line produced.</param>
    /// <returns>Success once recorded, or the refusal (<c>ORDER_LINE_CHANGED</c> for a lost race or a stale attempt).</returns>
    private async Task<Result> RecordOutcomeAsync(Order order, OrderItem item, int attempt, OrderLineRunOutcome outcome)
    {
        try
        {
            return await _bus.InvokeLineChangeAsync(
                new RecordOrderLineOutcomeCommand(
                    order.Id,
                    item.Id,
                    attempt,
                    outcome.Failed ? OrderLineOutcome.Failed : OrderLineOutcome.Fulfilled,
                    outcome.ErrorCode),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex, "Order {OrderId} item {ItemId}: recording the outcome of attempt {Attempt} threw.",
                order.Id, item.Id, attempt);
            return Result.FailureCode(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }
    }

    /// <summary>
    /// Checks that the attempt still holds the line and stamps the start of its action. A cancellation of the run is
    /// rethrown, so the caller releases the lines it had not finished.
    /// </summary>
    /// <param name="order">The order the line belongs to.</param>
    /// <param name="item">The line, as it stood after the claim.</param>
    /// <param name="attempt">The attempt number the claim gave the line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success when the action may run, or the refusal (<c>ORDER_LINE_CHANGED</c> when another attempt holds the line).</returns>
    private async Task<Result> StartActionAsync(Order order, OrderItem item, int attempt, CancellationToken ct)
    {
        try
        {
            return await _bus.InvokeLineChangeAsync(
                new StartOrderLineActionCommand(order.Id, item.Id, attempt), ct);
        }
        catch (Exception ex) when (!IsCancellation(ex, ct))
        {
            // Not knowing whether the claim still holds, the line is not run: it stays claimed, and a retry takes it once
            // the lease runs out. Running it could run it twice.
            _logger.LogError(
                ex, "Order {OrderId} item {ItemId}: the start of attempt {Attempt} could not be recorded.",
                order.Id, item.Id, attempt);
            return Result.FailureCode(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }
    }

    /// <summary>Runs one line through the use case its type needs.</summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The line.</param>
    /// <param name="attempt">The attempt number the claim gave the line.</param>
    /// <param name="orderDomainName">The domain bought beside the line in this order, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the line produced, or why it could not be fulfilled.</returns>
    private async Task<OrderLineRunOutcome> RunLineAsync(
        Order order, OrderItem item, int attempt, string? orderDomainName, CancellationToken ct)
    {
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
                return new OrderLineRunOutcome(
                    OrderLineKind.Domain, null, null, null, $"Order {order.Id} item {item.Id} has a domain action but no domain name.");
            }

            var domain = await FulfillDomainActionAsync(order, item, attempt, item.Domain, ct);
            return new OrderLineRunOutcome(OrderLineKind.Domain, domain.DomainId, null, domain.ErrorCode, domain.FailureReason);
        }

        var service = await FulfillServiceAsync(order, item, item.Domain ?? orderDomainName, ct);
        return service.IsFailure
            ? new OrderLineRunOutcome(OrderLineKind.Service, null, null, service.ErrorCode, DescribeFailure(service))
            : new OrderLineRunOutcome(OrderLineKind.Service, null, service.Value, null, null);
    }

    /// <summary>
    /// Records every line the run had not finished as failed because it was interrupted, so staff can retry it at once
    /// instead of waiting for a claim that nothing is holding to run out.
    /// </summary>
    /// <param name="order">The order the lines belong to.</param>
    /// <param name="unfinished">The line being run when the run was cancelled, and the lines after it.</param>
    /// <param name="attempts">The attempt number the claim gave each line, by line id.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ReleaseUnfinishedAsync(Order order, IReadOnlyList<OrderItem> unfinished, IReadOnlyDictionary<int, int> attempts)
    {
        foreach (var item in unfinished)
        {
            try
            {
                await _bus.InvokeLineChangeAsync(
                    new RecordOrderLineOutcomeCommand(
                        order.Id, item.Id, attempts[item.Id], OrderLineOutcome.Failed, HostpanelErrorCodes.ORDER_LINE_INTERRUPTED),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The cancellation is what the caller sees; a line that could not be released waits for its claim to run out.
                _logger.LogError(
                    ex, "Order {OrderId} item {ItemId} could not be released after the run was cancelled.", order.Id, item.Id);
            }
        }
    }

    /// <summary>Tells whether an exception is the caller cancelling, not a timeout or fault inside an action.</summary>
    /// <remarks>
    /// An HTTP call that times out throws <see cref="OperationCanceledException"/> too, with the caller's token still
    /// live; that is a failure of the line, not a cancellation of the run.
    /// </remarks>
    /// <param name="ex">The exception.</param>
    /// <param name="ct">The run's cancellation token.</param>
    /// <returns><see langword="true"/> when the run itself was cancelled.</returns>
    private static bool IsCancellation(Exception ex, CancellationToken ct)
    {
        return ex is OperationCanceledException && ct.IsCancellationRequested;
    }

    /// <summary>Tells which kind of line an order line is.</summary>
    /// <param name="item">The line.</param>
    /// <returns>Domain for a line with a domain action, otherwise Service.</returns>
    private static OrderLineKind KindOf(OrderItem item)
    {
        return item.DomainAction is null ? OrderLineKind.Service : OrderLineKind.Domain;
    }

    /// <summary>
    /// Creates the client service a product line bought, or finds the one an earlier attempt created. A refusal or a
    /// fault is logged loud and returned, so one failing line never aborts the rest.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The product line.</param>
    /// <param name="domain">The domain the service is for: the line's own, else one ordered beside it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service's id, or the failure the line ended with.</returns>
    private async Task<Result<int>> FulfillServiceAsync(Order order, OrderItem item, string? domain, CancellationToken ct)
    {
        // The service is linked to the line when it is created, so a retry gets back the service the first attempt made
        // (when it was ordered but not activated, or the run died before recording the line) instead of a second one
        // that would be provisioned and billed twice.
        var outcome = await _bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(
                order.ClientId, item.ProductId, item.BillingCycle,
                item.FirstPaymentAmount, item.RecurringAmount,
                order.PaymentMethod, domain, OrderItemId: item.Id), ct);

        if (outcome.IsFailure)
        {
            _logger.LogCritical(
                "Service provisioning failed for order {OrderId} item {ItemId} (product {ProductId}): {Reason}. " +
                "The order is accepted but this line was never provisioned and needs manual intervention.",
                order.Id, item.Id, item.ProductId, DescribeFailure(outcome));
            return outcome;
        }

        return await IsDigitalAsync(item.ProductId, ct) ? await ActivateDigitalAsync(order, item, outcome.Value, ct) : outcome;
    }

    /// <summary>
    /// Activates the service of a digital line, which has no hosting account to provision. A refusal is logged loud and
    /// returned, so one failing line never aborts the rest; the service is then left Pending for staff, and a retry of
    /// the line activates that same service.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The digital product line.</param>
    /// <param name="serviceId">The service the line created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service's id once it is Active, or the failure the line ended with.</returns>
    private async Task<Result<int>> ActivateDigitalAsync(Order order, OrderItem item, int serviceId, CancellationToken ct)
    {
        var activated = await _bus.InvokeAsync<Result>(new ActivateDigitalServiceCommand(serviceId, item.Id), ct);
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

    /// <summary>Asks the Products module whether a product is a digital one.</summary>
    /// <param name="productId">The line's product.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> for a digital product; a product the Products module does not know is not digital.</returns>
    private async Task<bool> IsDigitalAsync(int productId, CancellationToken ct)
    {
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(new GetProductsByIdsQuery([productId]), ct);
        return (products.Value ?? []).Any(p => p.Type == ProductType.Digital);
    }

    /// <summary>Runs the one domain action an order line carries.</summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="item">The line item, already known to carry a domain action.</param>
    /// <param name="attempt">The attempt number the claim gave the line.</param>
    /// <param name="domain">The domain name from the order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new domain's id when this action creates one (register, transfer) and it succeeded;
    /// otherwise no id. The failure reason is <see langword="null"/> on success and a
    /// human-readable sentence on failure.
    /// </returns>
    private async Task<DomainActionOutcome> FulfillDomainActionAsync(
        Order order, OrderItem item, int attempt, string domain, CancellationToken ct)
    {
        // A line's second attempt or later finds the domain this very order already created under that name and counts
        // it as done, so a first attempt that did register it (and failed afterwards) is never repeated. A domain the
        // client holds that this order did not create is not the line's registration.
        if (attempt > 1 && item.DomainAction is DomainAction.Register or DomainAction.Transfer)
        {
            if (await IsDomainHeldAsync(order, domain, ct))
            {
                _logger.LogInformation(
                    "Order {OrderId} item {ItemId}: domain '{Domain}' was already created for this order; the retry counts it as done.",
                    order.Id, item.Id, domain);
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
                    PriceCurrency: order.Currency,
                    OrderId: order.Id), ct);

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
                    Years: item.Years ?? 1,
                    OrderId: order.Id), ct);

            return outcome.IsFailure
                ? new DomainActionOutcome(null, outcome.ErrorCode, DescribeFailure(outcome))
                : new DomainActionOutcome(outcome.Value, null, null);
        }

        if (item.DomainAction == DomainAction.Renew)
        {
            // Extends a domain the client already owns; RenewOrderedDomainCommand refuses a domain that does not
            // exist or is not this client's, and counts the renewal as done when this same line already extended it
            // (the line id is stored with the renewal), so a retry never renews twice.
            var renewed = await _bus.InvokeAsync<Result>(
                new RenewOrderedDomainCommand(order.ClientId, domain, item.Years ?? 1, item.Id), ct);
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
    /// Asks the Domains module whether this order already created a live domain for its client under the line's name,
    /// which is what a first attempt that did register it (and died before recording the line) leaves behind. A domain
    /// the client holds from elsewhere, and an expired, cancelled or transferred-out one, is not a registration the line
    /// can count as done.
    /// </summary>
    /// <param name="order">The accepted order the line belongs to.</param>
    /// <param name="domain">The domain name from the order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when this order created the domain for the client and it is active or pending.</returns>
    private async Task<bool> IsDomainHeldAsync(Order order, string domain, CancellationToken ct)
    {
        var owned = await _bus.InvokeAsync<Result<IReadOnlyList<string>>>(
            new GetOwnedDomainNamesQuery(order.ClientId, [domain], LiveOnly: true, OrderId: order.Id), ct);
        return owned.IsSuccess && owned.Value!.Count > 0;
    }

    /// <summary>Builds the report of one line that could not be fulfilled.</summary>
    /// <param name="item">The failed line.</param>
    /// <param name="outcome">What running the line produced.</param>
    /// <param name="unrecordedAttempt">The attempt that still holds the line when its failure could not be recorded, else <see langword="null"/>.</param>
    /// <returns>The line's failure report.</returns>
    private static OrderLineFailureDto Failure(OrderItem item, OrderLineRunOutcome outcome, int? unrecordedAttempt) =>
        new(item.Id, outcome.Kind, item.ProductName, item.Domain, item.FirstPaymentAmount, outcome.ErrorCode, outcome.FailureReason!, unrecordedAttempt);

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
