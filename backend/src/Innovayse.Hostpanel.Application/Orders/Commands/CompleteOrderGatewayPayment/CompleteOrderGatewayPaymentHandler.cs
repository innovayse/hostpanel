using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;

/// <summary>
/// Resolves the invoice behind an order and verifies its hosted-gateway payment.
/// </summary>
/// <remarks>
/// Dispatches <see cref="CompleteGatewayPaymentCommand"/> rather than repeating it, so that
/// command's idempotency, an already-paid invoice answers Paid without touching the gateway,
/// keeps holding for the checkout return leg as well as for the reconciliation cron.
/// </remarks>
public sealed class CompleteOrderGatewayPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to reach the shared command once the invoice is known.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared command once the invoice is known.</param>
    public CompleteOrderGatewayPaymentHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order whose payment should be verified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The state the gateway reported for the session (paid, pending or declined) with the order's
    /// number, or
    /// <c>ORDER_NOT_FOUND</c> when no such order exists or the payment token does not match it,
    /// <c>ORDER_NOT_INVOICED</c> when the order was never billed, or whatever
    /// <see cref="CompleteGatewayPaymentCommand"/> itself refuses with (<c>INVOICE_NOT_FOUND</c>,
    /// <c>GATEWAY_SESSION_NOT_FOUND</c> or <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>).
    /// </returns>
    public async Task<Result<OrderGatewayCompletionDto>> HandleAsync(
        CompleteOrderGatewayPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var payable = await _context.FindPayableInvoicedOrderAsync(cmd.OrderId, cmd.PaymentToken, ct);
        if (payable.IsFailure)
        {
            return Result<OrderGatewayCompletionDto>.FailureCode(payable.ErrorCode!);
        }

        var order = payable.Value!;

        var completion = await _bus.InvokeAsync<Result<GatewayCompletionDto>>(
            new CompleteGatewayPaymentCommand(order.InvoiceId!.Value, cmd.ReturnParameters), ct);
        if (completion.IsFailure)
        {
            return Result<OrderGatewayCompletionDto>.FailureCode(completion.ErrorCode!);
        }

        return Result.Success(new OrderGatewayCompletionDto(completion.Value!.State, order.OrderNumber));
    }
}
