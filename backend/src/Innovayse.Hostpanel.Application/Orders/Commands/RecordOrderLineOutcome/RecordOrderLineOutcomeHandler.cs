using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;

/// <summary>
/// Handles <see cref="RecordOrderLineOutcomeCommand"/>: records on the line how an attempt ended. It loads the order
/// afresh, because the fulfilment that ran the attempt holds a copy loaded before other lines of the same order may have
/// finished, and a status computed from that copy could say Incomplete for an order whose every line is fulfilled.
/// </summary>
public sealed class RecordOrderLineOutcomeHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that dates the attempt's end.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock that dates the attempt's end.</param>
    public RecordOrderLineOutcomeHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order, the line and how the attempt ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the outcome is saved; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing;
    /// <c>ORDER_LINE_CHANGED</c> when the line is being or was refunded, or another writer saved the order first (the
    /// caller loads it again and records again).
    /// </returns>
    public async Task<Result> HandleAsync(RecordOrderLineOutcomeCommand cmd, CancellationToken ct)
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

        if (!order.CanRecordLineOutcome(cmd.ItemId))
        {
            // The refund overtook the attempt: the line is being or was given back, and must not become delivered.
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        if (cmd.Outcome == OrderLineOutcome.Fulfilled)
        {
            order.RecordLineFulfilled(cmd.ItemId, _clock.UtcNow);
        }
        else
        {
            order.RecordLineFailed(cmd.ItemId, cmd.ErrorCode, _clock.UtcNow);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another writer saved the line or the order between the load and the save; nothing of this attempt was
            // written, so the caller loads the order again and records again.
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success();
    }
}
