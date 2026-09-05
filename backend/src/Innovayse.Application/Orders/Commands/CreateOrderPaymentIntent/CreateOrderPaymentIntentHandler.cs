namespace Innovayse.Application.Orders.Commands.CreateOrderPaymentIntent;

using Innovayse.Application.Orders.Extensions;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Orders.Interfaces;

/// <summary>
/// Handles <see cref="CreateOrderPaymentIntentCommand"/>: resolves the order's invoice, and
/// registers an intent to charge its total at the card gateway.
/// </summary>
/// <remarks>
/// This is the half that used to sit in the orders controller, which loaded both aggregates,
/// built the gateway's metadata and called the Stripe client itself -- an endpoint holding a
/// use case, and the API project reaching an integration directly.
/// </remarks>
/// <param name="orderRepo">Order repository.</param>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="stripeService">The card gateway this backend opens intents at.</param>
public sealed class CreateOrderPaymentIntentHandler(
    IOrderRepository orderRepo,
    IInvoiceRepository invoiceRepo,
    IStripeService stripeService)
{
    /// <summary>
    /// Currency every card intent is opened in. Was a bare literal at the call site this
    /// replaced; it is named here so the one place it is decided is greppable, and left
    /// unchanged because moving a use case is not the change that should also reprice it.
    /// </summary>
    private const string IntentCurrency = "usd";

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order to open a payment intent for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The gateway's client secret. It is what the payer's browser confirms the charge with, and
    /// it is useless to anyone who does not also hold the publishable key.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no such order exists or its payment token does not match, when the order was
    /// never billed, and when its invoice has gone missing.
    /// </exception>
    public async Task<string> HandleAsync(CreateOrderPaymentIntentCommand cmd, CancellationToken ct)
    {
        var order = (await orderRepo.FindByIdAsync(cmd.OrderId, ct))
            .EnsurePayableWith(cmd.OrderId, cmd.PaymentToken);

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {cmd.OrderId} has no linked invoice.");
        }

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        // Carried so the gateway's own webhook and dashboard name the same rows this database
        // does -- a payment that arrives with no way back to an order is a manual reconciliation.
        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(),
            ["invoiceId"] = invoice.Id.ToString(),
            ["clientId"] = order.ClientId.ToString(),
        };

        return await stripeService.CreatePaymentIntentAsync(invoice.Total, IntentCurrency, metadata, ct);
    }
}
