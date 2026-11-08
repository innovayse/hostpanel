using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;

/// <summary>
/// Handles <see cref="BeginOrderLineRefundCommand"/>: moves a failed line to Refunding. The claim is saved before the
/// refund starts, and a retry that claimed the line first (or claims it at the same moment) wins or loses on the line's
/// concurrency token, so a line is never both delivered and refunded.
/// </summary>
public sealed class BeginOrderLineRefundHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public BeginOrderLineRefundHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and the failed line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the line is Refunding; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing;
    /// <c>ORDER_LINE_NOT_REFUNDABLE</c> when the line is not failed (a retry holds it, or it is already settled);
    /// <c>ORDER_LINE_CHANGED</c> when another writer saved the line at the same moment.
    /// </returns>
    public async Task<Result> HandleAsync(BeginOrderLineRefundCommand cmd, CancellationToken ct)
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

        if (!order.CanRefundLine(cmd.ItemId))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        }

        order.BeginLineRefund(cmd.ItemId);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A retry claimed the line, or another writer saved the order, at the same moment.
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success();
    }
}
