using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CancelOrder;

/// <summary>
/// Cancels a pending order, transitioning it to
/// <see cref="Innovayse.Hostpanel.Domain.Orders.Enums.OrderStatus.Cancelled"/>. An invoice the
/// order still has open is cancelled first through Billing's <see cref="CancelInvoiceCommand"/>,
/// so no invoice is left payable for an order that will never be fulfilled.
/// </summary>
public sealed class CancelOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the linked invoice's brief and its cancellation; Orders reads no Billing table.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the linked invoice's brief and its cancellation.</param>
    public CancelOrderHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>ORDER_NOT_FOUND</c> when the id names no order, or <c>ORDER_NOT_PENDING</c>
    /// when it is not Pending, or whichever code <see cref="CancelInvoiceCommand"/> refuses the
    /// linked invoice with (an invoice that already took a payment is never cancelled, so its
    /// order is left unchanged).
    /// </returns>
    public async Task<Result> HandleAsync(CancelOrderCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        // Checked before the invoice is touched, so a refused cancel never cancels the invoice.
        if (order.Status is not OrderStatus.Pending)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_PENDING);
        }

        if (order.InvoiceId is { } invoiceId)
        {
            // Billing refuses to cancel an invoice that already took money, so the order of a paid invoice stays as it is.
            var cancelled = await _bus.CancelOpenInvoiceAsync(invoiceId, ct);
            if (cancelled.IsFailure)
            {
                return cancelled;
            }
        }

        order.Cancel();
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
