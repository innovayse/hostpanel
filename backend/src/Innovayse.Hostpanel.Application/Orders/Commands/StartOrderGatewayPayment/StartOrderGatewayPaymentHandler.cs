using Innovayse.Hostpanel.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderGatewayPayment;

/// <summary>
/// Resolves the invoice behind an order and starts a hosted-gateway payment on it.
/// </summary>
/// <remarks>
/// Dispatches <see cref="StartGatewayPaymentCommand"/> rather than repeating its work, the same
/// way <c>StartMyGatewayPaymentHandler</c> does for a client's own invoice: every check that
/// command makes, payability, the allowed return-URL origins, the live-session window, the
/// currency match, stays in one place, and this handler adds only the order-to-invoice step.
/// </remarks>
public sealed class StartOrderGatewayPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to reach the shared command once the invoice is known.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared command once the invoice is known.</param>
    public StartOrderGatewayPaymentHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to pay, and how.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The absolute gateway URL to redirect the payer's browser to, or <c>ORDER_NOT_FOUND</c> when
    /// no such order exists or the payment token does not match it, <c>ORDER_NOT_INVOICED</c> when
    /// the order was never billed, or whatever <see cref="StartGatewayPaymentCommand"/> itself
    /// refuses with (<c>INVOICE_NOT_FOUND</c>, <c>INVOICE_NOT_PAYABLE</c>,
    /// <c>RETURN_URL_NOT_ALLOWED</c>, <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>,
    /// <c>GATEWAY_SESSION_ALREADY_STARTED</c>, <c>CURRENCY_NOT_FOUND</c> or
    /// <c>GATEWAY_CURRENCY_MISMATCH</c>).
    /// </returns>
    public async Task<Result<RedirectDto>> HandleAsync(StartOrderGatewayPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var payable = await _context.FindPayableInvoicedOrderAsync(cmd.OrderId, cmd.PaymentToken, ct);
        if (payable.IsFailure)
        {
            return Result<RedirectDto>.FailureCode(payable.ErrorCode!);
        }

        var order = payable.Value!;

        return await _bus.InvokeAsync<Result<RedirectDto>>(
            new StartGatewayPaymentCommand(
                order.InvoiceId!.Value, cmd.Module, cmd.ReturnUrl, cmd.SavePaymentMethod, cmd.ConsentTextVersion, cmd.IpAddress, cmd.UserAgent),
            ct);
    }
}
