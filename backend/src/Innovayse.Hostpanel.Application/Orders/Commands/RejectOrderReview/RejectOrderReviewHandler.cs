using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Application.Fraud.Extensions;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RejectOrderReview;

/// <summary>
/// Refuses an order held for fraud review: its open invoice is cancelled through Billing, the order becomes
/// <see cref="OrderStatus.Fraud"/> (its lines cancelled, the client mailed the reason by the event the order raises) and the staff
/// decision is written to the fraud log. An invoice that already took a payment is not cancelled, so the order stays under review
/// until staff have refunded it.
/// </summary>
public sealed class RejectOrderReviewHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the invoice, the client and the fraud log.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the current moment, stamped on the order's event.</summary>
    private readonly IClock _clock;

    /// <summary>Logs a fraud log line that could not be written.</summary>
    private readonly ILogger<RejectOrderReviewHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the invoice, the client and the fraud log.</param>
    /// <param name="clock">Reads the current moment, stamped on the order's event.</param>
    /// <param name="logger">Logs a fraud log line that could not be written.</param>
    public RejectOrderReviewHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock, ILogger<RejectOrderReviewHandler> logger)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to reject and the reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>ORDER_NOT_FOUND</c> when the id names no order, or <c>ORDER_NOT_UNDER_REVIEW</c> when it is not held for review,
    /// or whichever code Billing refuses the invoice's cancellation with (a paid invoice is refused, and the order is left as it is).
    /// </returns>
    public async Task<Result> HandleAsync(RejectOrderReviewCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        // Checked before the invoice is touched, so a refused rejection never cancels the invoice.
        if (order.Status is not OrderStatus.UnderReview)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_UNDER_REVIEW);
        }

        if (order.InvoiceId is { } invoiceId)
        {
            var cancelled = await _bus.CancelOpenInvoiceAsync(invoiceId, ct);
            if (cancelled.IsFailure)
            {
                return cancelled;
            }
        }

        order.RejectFromReview(cmd.Reason, _clock.UtcNow);
        await _context.SaveChangesAsync(ct);

        var client = await _bus.FindClientBriefAsync(order.ClientId, withEmail: true, ct);
        await _bus.RecordFraudDecisionAsync(
            _logger,
            new RecordFraudDecisionCommand(
                FraudCheckKind.Order, FraudDecision.ManualRejected, order.ClientId, order.Id, order.IpAddress, client?.Email,
                order.FraudScore ?? 0, order.FraudReasons),
            ct);

        return Result.Success();
    }
}
