using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Application.Fraud.Extensions;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ApproveOrderReview;

/// <summary>
/// Releases an order held for fraud review back to <see cref="OrderStatus.Pending"/>, writes the staff decision to the fraud log
/// and, when the order's invoice was already paid while it waited, fulfils it at once through
/// <see cref="FulfillPaidOrderCommand"/> (an unpaid order is fulfilled by its payment, as any pending order is).
/// </summary>
public sealed class ApproveOrderReviewHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the invoice, the client, the fraud log and the fulfilment.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Logs a fulfilment that was refused after the approval was saved.</summary>
    private readonly ILogger<ApproveOrderReviewHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the invoice, the client, the fraud log and the fulfilment.</param>
    /// <param name="logger">Logs a fulfilment that was refused after the approval was saved.</param>
    public ApproveOrderReviewHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<ApproveOrderReviewHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to approve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>ORDER_NOT_FOUND</c> when the id names no order, or <c>ORDER_NOT_UNDER_REVIEW</c> when it is not held for review.</returns>
    public async Task<Result> HandleAsync(ApproveOrderReviewCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (order.Status is not OrderStatus.UnderReview)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_UNDER_REVIEW);
        }

        order.ApproveReview();
        await _context.SaveChangesAsync(ct);

        var client = await _bus.FindClientBriefAsync(order.ClientId, withEmail: true, ct);
        await _bus.RecordFraudDecisionAsync(
            _logger,
            new RecordFraudDecisionCommand(
                FraudCheckKind.Order, FraudDecision.ManualApproved, order.ClientId, order.Id, order.IpAddress, client?.Email,
                order.FraudScore ?? 0, order.FraudReasons),
            ct);

        // A payment that arrived while the order was held fulfilled nothing, and nothing will announce it again.
        if (order.InvoiceId is { } invoiceId && await _bus.IsInvoicePaidAsync(invoiceId, ct))
        {
            var fulfilled = await _bus.InvokeAsync<Result>(new FulfillPaidOrderCommand(order.Id), ct);
            if (fulfilled.IsFailure)
            {
                // The approval is saved and stays; staff can accept the pending order by hand.
                _logger.LogError("Approved order {OrderId} could not be fulfilled: {ErrorCode}.", order.Id, fulfilled.ErrorCode);
            }
        }

        return Result.Success();
    }
}
