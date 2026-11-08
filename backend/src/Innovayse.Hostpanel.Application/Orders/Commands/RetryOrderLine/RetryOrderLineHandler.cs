using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RetryOrderLine;

/// <summary>
/// Handles <see cref="RetryOrderLineCommand"/>: claims one failed line of an accepted order (the claim is saved,
/// with its audit line, before any work starts, so two retries of the same line cannot both run) and then runs
/// the line through <see cref="FulfillOrderLinesCommand"/>, the fulfilment the first attempt used.
/// </summary>
public sealed class RetryOrderLineHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, used to fulfil the claimed line.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock that dates the claim.</summary>
    private readonly IClock _clock;

    /// <summary>Writes the audit line of the retry.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>How long a retry's claim on a line is respected; the <c>Orders:RetryLeaseMinutes</c> option.</summary>
    private readonly IOptions<OrderOptions> _options;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<RetryOrderLineHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus, used to fulfil the claimed line.</param>
    /// <param name="clock">The clock that dates the claim.</param>
    /// <param name="activityLogger">Writes the audit line of the retry.</param>
    /// <param name="options">The order options, which hold how long a retry's claim on a line is respected.</param>
    /// <param name="logger">Structured logger.</param>
    public RetryOrderLineHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activityLogger,
        IOptions<OrderOptions> options,
        ILogger<RetryOrderLineHandler> logger)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _activityLogger = activityLogger;
        _options = options;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and the line to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The lines that are still failed after the retry (empty when the line was fulfilled); <c>ORDER_NOT_FOUND</c> or
    /// <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing; <c>ORDER_LINE_ALREADY_FULFILLED</c> when the line is done
    /// already; <c>ORDER_LINE_NOT_RETRYABLE</c> when the order was not accepted, the line did not fail, is being or was
    /// refunded, or another retry holds it; <c>ORDER_LINE_CHANGED</c> when the line changed while the retry ran and its
    /// outcome could not be recorded.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The caller cancelled after the line was claimed; the line is recorded as interrupted first, so it can be retried at once.
    /// </exception>
    public async Task<Result<OrderFulfillmentDto>> HandleAsync(RetryOrderLineCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        var item = order.Items.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (item is null)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        if (item.FulfilmentState is OrderItemFulfilmentState.Fulfilled)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_LINE_ALREADY_FULFILLED);
        }

        var now = _clock.UtcNow;
        var lease = _options.Value.RetryLease;
        if (!order.CanRetryLine(item.Id, now, lease))
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
        }

        order.StartLineRetry(item.Id, now, lease);
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                order.ClientId,
                $"Retried Order Line - Order ID: {order.Id} - Line ID: {item.Id} - Attempt: {item.FulfilmentAttempts}"),
            ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A competing retry claimed the line first; it is the one that runs.
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
        }

        var attempt = item.FulfilmentAttempts;
        try
        {
            return await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(new FulfillOrderLinesCommand(order.Id, item.Id), ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            // Cancelled after the claim was saved: the run may have stopped before it recorded anything, which would leave
            // the line claimed until the lease runs out. Recording it as interrupted lets staff retry at once; when the
            // run already did that itself, the line is not in progress any more and this record is refused, which is fine.
            await ReleaseClaimAsync(order.Id, item.Id, attempt);
            throw;
        }
    }

    /// <summary>
    /// Records the claimed line as interrupted, so a retry cancelled after its claim does not leave the line in progress.
    /// Not cancellable: the caller already cancelled, and the line must still say so.
    /// </summary>
    /// <param name="orderId">The order the line belongs to.</param>
    /// <param name="itemId">The claimed line.</param>
    /// <param name="attempt">The attempt number the claim gave the line.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ReleaseClaimAsync(int orderId, int itemId, int attempt)
    {
        try
        {
            await _bus.InvokeLineChangeAsync(
                new RecordOrderLineOutcomeCommand(
                    orderId, itemId, attempt, OrderLineOutcome.Failed, HostpanelErrorCodes.ORDER_LINE_INTERRUPTED),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The cancellation is what the caller sees; a line that could not be released waits for its claim to run out.
            _logger.LogError(
                ex, "Order {OrderId} item {ItemId} could not be released after the retry was cancelled.", orderId, itemId);
        }
    }
}
