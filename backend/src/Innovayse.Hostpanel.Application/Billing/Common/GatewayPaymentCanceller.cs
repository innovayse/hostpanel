using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place the host voids a hosted-gateway payment it abandons: the session of a cancelled invoice, and the session a
/// new attempt replaces. It asks the plugin that registered the payment to cancel it, when that plugin implements
/// <see cref="IPaymentCancelPlugin"/>, so the payer cannot complete a payment the host no longer waits for. A plugin without
/// the capability, a plugin that is no longer enabled and a session that already took money leave the gateway order as it is,
/// as before.
/// </summary>
/// <remarks>
/// Cancelling is best effort and never changes what the caller does: it runs after the caller's own change is saved, and a
/// plugin that throws is logged and ignored, because an order the gateway still holds expires on its own and the
/// reconciler never settles a session the invoice no longer points at.
/// </remarks>
public sealed class GatewayPaymentCanceller
{
    /// <summary>Resolves the payment plugin that registered the abandoned order.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>Structured logger; records the outcome of a cancel and a plugin that threw.</summary>
    private readonly ILogger<GatewayPaymentCanceller> _logger;

    /// <summary>Initializes a new instance of the <see cref="GatewayPaymentCanceller"/> class.</summary>
    /// <param name="pluginResolver">Resolves the payment plugin that registered the abandoned order.</param>
    /// <param name="logger">Structured logger.</param>
    public GatewayPaymentCanceller(IPaymentPluginResolver pluginResolver, ILogger<GatewayPaymentCanceller> logger)
    {
        _pluginResolver = pluginResolver;
        _logger = logger;
    }

    /// <summary>Cancels the gateway order an invoice's session registered, unless that session took a payment.</summary>
    /// <param name="invoice">The invoice, with its transactions loaded, that holds the session being abandoned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the plugin answered, or at once when there is nothing to cancel.</returns>
    public Task CancelSessionOfAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        // A session that took money is a settled payment, not an abandoned one; the refund flow handles it.
        return invoice is { GatewayModule: { } module, GatewayOrderId: { } orderId } && !invoice.WasPaidThrough(module)
            ? CancelAsync(module, orderId, invoice.Id, ct)
            : Task.CompletedTask;
    }

    /// <summary>Cancels one gateway order through the plugin that registered it, when the plugin can.</summary>
    /// <param name="module">The plugin id of the session being abandoned.</param>
    /// <param name="gatewayOrderId">The gateway-side order of that session.</param>
    /// <param name="invoiceId">The invoice, for the log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the plugin answered, or at once when it cannot cancel.</returns>
    public async Task CancelAsync(string module, string gatewayOrderId, int invoiceId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayOrderId);

        var plugin = await _pluginResolver.ResolveAsync(module, ct);
        if (plugin is not IPaymentCancelPlugin canceller)
        {
            return;
        }

        try
        {
            var result = await canceller.CancelPaymentAsync(gatewayOrderId, ct);
            if (result.Outcome == PaymentCancelOutcome.NotCancellable)
            {
                _logger.LogWarning(
                    "Payment plugin {Module} could not cancel the abandoned order of invoice {InvoiceId}.", module, invoiceId);
            }
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // Nothing the caller did depends on this; an order the gateway keeps expires on its own.
            _logger.LogWarning(
                ex, "Payment plugin {Module} failed to cancel the abandoned order of invoice {InvoiceId}.", module, invoiceId);
        }
    }
}
