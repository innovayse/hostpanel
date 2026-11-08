using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CloseOrderLineRefund;

/// <summary>
/// Handles <see cref="CloseOrderLineRefundCommand"/>: a partly refunded line is closed to retries and keeps its order
/// incomplete until staff finish the refund by hand; this is how they tell the system it is finished. The line becomes
/// Refunded, the order's status follows its lines, and the action is written to the client's activity log in the same save.
/// </summary>
public sealed class CloseOrderLineRefundHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Writes the audit line of the action.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activityLogger">Writes the audit line of the action.</param>
    public CloseOrderLineRefundHandler(IHostpanelDbContext context, IActivityLogger activityLogger)
    {
        _context = context;
        _activityLogger = activityLogger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and the partly refunded line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the line is Refunded; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing;
    /// <c>ORDER_LINE_NOT_REFUNDABLE</c> when the line is not partly refunded; <c>ORDER_LINE_CHANGED</c> when another writer
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

        if (!order.CanCloseLineRefund(cmd.ItemId))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        }

        order.CloseLineRefund(cmd.ItemId);
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                order.ClientId,
                $"Closed Order Line Refund - Order ID: {order.Id} - Line ID: {cmd.ItemId}"),
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
