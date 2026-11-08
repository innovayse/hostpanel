using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineRefunded;

/// <summary>
/// Handles <see cref="RecordOrderLineRefundedCommand"/>: closes a failed line whose cost was given back and
/// lets the order's status follow its lines. It loads the order afresh, because the fulfilment that failed the
/// line ran in its own scope and saved its own copy.
/// </summary>
public sealed class RecordOrderLineRefundedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public RecordOrderLineRefundedHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and the refunded line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the line is closed (a line that is not failed is left as it is, still Success), or
    /// <c>ORDER_NOT_FOUND</c> / <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing.
    /// </returns>
    public async Task<Result> HandleAsync(RecordOrderLineRefundedCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        var item = order.Items.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (item is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        // Only a failed line has a cost to give back; a fulfilled one was delivered and stays so.
        if (item.FulfilmentState is OrderItemFulfilmentState.Failed)
        {
            order.RecordLineRefunded(item.Id);
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
