using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderLineAction;

/// <summary>
/// Handles <see cref="StartOrderLineActionCommand"/>. A run over the whole order claims every line at accept with one
/// timestamp and runs them one after another, so without this a line still waiting its turn could reach the end of its
/// lease, be taken over by a staff retry, and then be run a second time by the first run. Checking the claim on the line
/// as the database holds it now, and saving a new stamp under the line's concurrency token, means a line is started by
/// exactly one attempt: the one that still holds it.
/// </summary>
public sealed class StartOrderLineActionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that dates the start of the action.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock that dates the start of the action.</param>
    public StartOrderLineActionHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order, the line and the attempt that claimed it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the stamp is saved, and the action may run; <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when
    /// an id names nothing; <c>ORDER_LINE_CHANGED</c> when the attempt no longer holds the line (a retry took it over, or
    /// a refund) or another writer saved the order first. Only Success lets the action run.
    /// </returns>
    public async Task<Result> HandleAsync(StartOrderLineActionCommand cmd, CancellationToken ct)
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

        if (!order.CanRecordLineOutcome(cmd.ItemId, cmd.Attempt))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        order.StartLineAction(cmd.ItemId, cmd.Attempt, _clock.UtcNow);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A retry claimed the line, or another writer saved the order, between the load and the save.
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success();
    }
}
