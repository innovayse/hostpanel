using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.AcceptOrder;

/// <summary>
/// Accepts a pending order, transitions it to Active, and fulfils every line through
/// <see cref="FulfillOrderLinesCommand"/>, the same fulfilment a paid order gets: a domain line is
/// registered, transferred or renewed, a product line becomes a client service. Every line is
/// tried; the ones that could not be fulfilled are reported back rather than stopping the rest.
/// </summary>
public sealed class AcceptOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, used to fulfil the order's lines.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus, used to fulfil the order's lines.</param>
    /// <param name="clock">Reads the current moment.</param>
    public AcceptOrderHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The lines that could not be fulfilled (empty when every line was), or <c>ORDER_NOT_FOUND</c>
    /// when the id names no order, or <c>ORDER_LINE_CHANGED</c> when the lines could not be claimed because the order kept
    /// changing under the run. The order's own status invariant (must be Pending) still throws;
    /// see <see cref="Innovayse.Hostpanel.Domain.Orders.Order.Accept"/>.
    /// </returns>
    public async Task<Result<OrderFulfillmentDto>> HandleAsync(AcceptOrderCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result<OrderFulfillmentDto>.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        // The lines are claimed in the same save that accepts the order, so a crash after it leaves them claimed and the
        // order Incomplete (retryable once the claim runs out) instead of Active over lines that never started.
        var now = _clock.UtcNow;
        order.Accept(now);
        var claimed = order.BeginFulfilment(now);
        await _context.SaveChangesAsync(ct);

        return await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(
            new FulfillOrderLinesCommand(order.Id, Claimed: claimed.ToClaimedLines()), ct);
    }
}
