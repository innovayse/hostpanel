using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;

/// <summary>
/// Handles <see cref="ClaimOrderLinesCommand"/>: claims the pending and failed lines of an accepted order in one save, on
/// the order as the database holds it now, and answers with exactly the lines it claimed and their attempt numbers. A line
/// another run already claimed is left to that run and is not in the answer.
/// </summary>
public sealed class ClaimOrderLinesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that dates the claim.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock that dates the claim.</param>
    public ClaimOrderLinesHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order and, optionally, the one line to claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The lines this claim took, each with its attempt number, once the claim is saved (empty when there was nothing to
    /// claim); <c>ORDER_NOT_FOUND</c> or
    /// <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing; <c>ORDER_NOT_ACCEPTED</c> when the order is pending, cancelled
    /// or fraud; <c>ORDER_LINE_CHANGED</c> when another writer saved the order first (the caller sends the claim again).
    /// </returns>
    public async Task<Result<IReadOnlyList<ClaimedOrderLineDto>>> HandleAsync(ClaimOrderLinesCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result<IReadOnlyList<ClaimedOrderLineDto>>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (cmd.ItemId is { } itemId && order.Items.All(i => i.Id != itemId))
        {
            return Result<IReadOnlyList<ClaimedOrderLineDto>>.FailureCode(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
        }

        if (!order.IsAccepted)
        {
            return Result<IReadOnlyList<ClaimedOrderLineDto>>.FailureCode(HostpanelErrorCodes.ORDER_NOT_ACCEPTED);
        }

        var claimed = order.BeginFulfilment(_clock.UtcNow, cmd.ItemId);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another run or a staff action saved the order between the load and the save; nothing was claimed.
            return Result<IReadOnlyList<ClaimedOrderLineDto>>.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        }

        return Result.Success(claimed.ToClaimedLines());
    }
}
