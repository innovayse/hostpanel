namespace Innovayse.Application.Billing.Commands.StartGatewayPayment;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.SDK.Plugins;

/// <summary>
/// Handles <see cref="StartGatewayPaymentCommand"/>: resolves the payment plugin,
/// registers the payment (fresh per-attempt order number — the gateway rejects
/// reused ones), stores the session on the invoice, and returns the redirect URL.
/// </summary>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="pluginResolver">Payment plugin resolver.</param>
/// <param name="uow">Unit of work.</param>
public sealed class StartGatewayPaymentHandler(
    IInvoiceRepository invoiceRepo,
    IPaymentPluginResolver pluginResolver,
    IUnitOfWork uow)
{
    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The start command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway redirect URL for the payer's browser.</returns>
    /// <exception cref="InvalidOperationException">Invoice not found/payable, or plugin unavailable.</exception>
    public async Task<string> HandleAsync(StartGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await invoiceRepo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        var plugin = await pluginResolver.ResolveAsync(cmd.Module, ct)
            ?? throw new InvalidOperationException($"Payment method '{cmd.Module}' is not available.");

        // Unique per attempt (gateway errorCode 1 on reuse); ≤ 25 chars fits int ids + unix seconds.
        var orderNumber = $"INV{invoice.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // The Application layer owns the major→minor conversion (ISO 4217 exponent-2).
        var amountMinor = (long)(invoice.Total * 100);

        var session = await plugin.CreatePaymentAsync(
            new PaymentRequest(orderNumber, amountMinor, cmd.ReturnUrl, $"Invoice #{invoice.Id}", null),
            ct);

        invoice.SetGatewaySession(cmd.Module, session.GatewayOrderId);
        await uow.SaveChangesAsync(ct);

        return session.RedirectUrl;
    }
}
