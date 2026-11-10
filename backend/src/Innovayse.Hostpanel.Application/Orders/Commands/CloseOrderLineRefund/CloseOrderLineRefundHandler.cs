using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CloseOrderLineRefund;

/// <summary>
/// Handles <see cref="CloseOrderLineRefundCommand"/>: how staff tell the system they gave a line's cost back by hand. A
/// failed line they refunded themselves, a line whose refund has been stuck in Refunding for longer than the retry lease,
/// and a partly refunded line all keep the order incomplete until then. The line becomes Refunded, the order's status
/// follows its lines, and the action, with the
/// state the line was closed from, is written to the client's activity log in the same save. The admin asks for a
/// confirmation before it sends this.
/// </summary>
public sealed class CloseOrderLineRefundHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Writes the audit line of the action.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>The retry lease, the <c>Orders:RetryLeaseMinutes</c> option; a refund younger than it may still be running.</summary>
    private readonly IOptions<OrderOptions> _options;

    /// <summary>The clock a refund's lease is measured against.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activityLogger">Writes the audit line of the action.</param>
    /// <param name="options">The retry lease, the <c>Orders:RetryLeaseMinutes</c> option.</param>
    /// <param name="clock">The clock a refund's lease is measured against.</param>
    public CloseOrderLineRefundHandler(
        IHostpanelDbContext context,
        IActivityLogger activityLogger,
        IOptions<OrderOptions> options,
        IClock clock)
    {
        _context = context;
        _activityLogger = activityLogger;
        _options = options;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and the failed, refunding or partly refunded line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the line is Refunded; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing;
    /// <c>ORDER_LINE_REFUND_IN_PROGRESS</c> when its refund began less than the retry lease ago and may still be giving the
    /// money back; <c>ORDER_LINE_NOT_REFUNDABLE</c> when the line is in another state; <c>ORDER_LINE_CHANGED</c> when another writer
    /// saved the order first (the caller opens the order again).
    /// </returns>
    public async Task<Result> HandleAsync(CloseOrderLineRefundCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (order.Items.All(i => i.Id != cmd.ItemId))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        // A refund inside its lease may be moving the money right now; closing it by hand could refund the line twice.
        var now = _clock.UtcNow;
        var lease = _options.Value.RetryLease;
        if (order.HasRunningLineRefund(cmd.ItemId, now, lease))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_REFUND_IN_PROGRESS);
        }

        if (!order.CanCloseLineRefund(cmd.ItemId, now, lease))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        }

        var closedFrom = order.Items.First(i => i.Id == cmd.ItemId).FulfilmentState;
        order.CloseLineRefund(cmd.ItemId, now, lease);
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                order.ClientId,
                $"Closed Order Line Refund - Order ID: {order.Id} - Line ID: {cmd.ItemId} - From: {closedFrom}"),
            ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success();
    }
}
