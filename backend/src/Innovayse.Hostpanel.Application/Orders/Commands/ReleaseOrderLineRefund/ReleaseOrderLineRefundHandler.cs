using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;

/// <summary>
/// Handles <see cref="ReleaseOrderLineRefundCommand"/>: ends a refund that did not give back the whole cost, either back
/// to Failed (nothing was returned) or to PartiallyRefunded (some was, and staff finish it by hand).
/// </summary>
public sealed class ReleaseOrderLineRefundHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ReleaseOrderLineRefundHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order, the line and how far the refund got.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the line is settled; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing;
    /// <c>ORDER_LINE_NOT_REFUNDABLE</c> when the line is not being refunded; <c>ORDER_LINE_CHANGED</c> when another writer
    /// saved the order first (the caller loads it again and settles again).
    /// </returns>
    public async Task<Result> HandleAsync(ReleaseOrderLineRefundCommand cmd, CancellationToken ct)
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

        if (!order.IsLineBeingRefunded(cmd.ItemId))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        }

        order.RecordLineRefundReleased(cmd.ItemId, cmd.Outcome == OrderLineRefundOutcome.PartlyRefunded);
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
