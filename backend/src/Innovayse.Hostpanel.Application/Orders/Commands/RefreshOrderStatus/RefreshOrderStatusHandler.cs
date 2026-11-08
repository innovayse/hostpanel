using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RefreshOrderStatus;

/// <summary>
/// Handles <see cref="RefreshOrderStatusCommand"/>: loads the order and its lines afresh and brings its status in line
/// with them. The last of several lines to finish always sees the others finished, so it settles the status, however the
/// runs interleaved.
/// </summary>
public sealed class RefreshOrderStatusHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public RefreshOrderStatusHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to refresh.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (an order whose status already fits its lines is left as it is); <c>ORDER_NOT_FOUND</c> when the id names
    /// no order; <c>ORDER_LINE_CHANGED</c> when another writer saved the order first (the caller refreshes again).
    /// </returns>
    public async Task<Result> HandleAsync(RefreshOrderStatusCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        order.RefreshStatus();

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another refresh or a staff action saved the order first; the caller loads it again.
            return Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success();
    }
}
