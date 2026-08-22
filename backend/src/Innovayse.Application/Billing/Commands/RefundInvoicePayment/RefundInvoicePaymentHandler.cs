namespace Innovayse.Application.Billing.Commands.RefundInvoicePayment;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Records a refund against a paid invoice, creates a client-level transaction ledger entry,
/// and optionally adds the refund amount to the client's credit balance (for CreditBalance refunds).
/// For invoices paid through a hosted-gateway payment plugin, the gateway refund is executed
/// first and the refund is recorded only on success.
/// </summary>
public sealed class RefundInvoicePaymentHandler(
    IInvoiceRepository repo,
    ITransactionRepository transactionRepo,
    IClientRepository clientRepo,
    IPaymentPluginResolver pluginResolver,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="RefundInvoicePaymentCommand"/>.
    /// </summary>
    /// <param name="cmd">The refund command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the refund is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found or not in Paid status.</exception>
    public async Task HandleAsync(RefundInvoicePaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        // Determine the actual refund amount (0 means full refund of the original transaction)
        var refundAmount = cmd.Amount > 0 ? cmd.Amount : invoice.Total;

        // Determine the transaction reference based on refund type
        var txnRef = cmd.RefundType switch
        {
            "Manual" => cmd.RefundTransactionId ?? $"manual-refund-{cmd.InvoiceId}-{DateTimeOffset.UtcNow.Ticks}",
            "CreditBalance" => $"credit-refund-{cmd.InvoiceId}-{DateTimeOffset.UtcNow.Ticks}",
            _ => await ExecuteGatewayRefundAsync(invoice, refundAmount, ct),
        };

        var gateway = cmd.RefundType == "CreditBalance" ? "Credit Balance" : cmd.Gateway;
        var addToCredit = cmd.RefundType == "CreditBalance";

        invoice.AddRefund(DateTimeOffset.UtcNow, gateway, txnRef, refundAmount, 0m, cmd.Notes);

        var clientTx = Transaction.Create(
            invoice.ClientId,
            DateTimeOffset.UtcNow,
            $"Invoice #{cmd.InvoiceId} Refund",
            txnRef,
            cmd.InvoiceId,
            gateway,
            0m,
            refundAmount,
            0m,
            addedToCredit: addToCredit);
        transactionRepo.Add(clientTx);

        if (addToCredit)
        {
            var client = await clientRepo.FindByIdAsync(invoice.ClientId, ct)
                ?? throw new InvalidOperationException($"Client {invoice.ClientId} not found.");
            client.AddCredit(refundAmount);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Executes a real gateway refund when the invoice was paid through a hosted-gateway
    /// plugin; falls back to a bookkeeping-only reference otherwise (legacy behavior).
    /// </summary>
    /// <param name="invoice">The invoice being refunded.</param>
    /// <param name="refundAmount">The refund amount in major units.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transaction reference to record.</returns>
    private async Task<string> ExecuteGatewayRefundAsync(
        Invoice invoice, decimal refundAmount, CancellationToken ct)
    {
        if (invoice.GatewayModule is null || invoice.GatewayOrderId is null)
        {
            return $"gateway-refund-{invoice.Id}-{DateTimeOffset.UtcNow.Ticks}";
        }

        var plugin = await pluginResolver.ResolveAsync(invoice.GatewayModule, ct)
            ?? throw new InvalidOperationException(
                $"Payment plugin '{invoice.GatewayModule}' is not configured; cannot refund invoice {invoice.Id}.");

        // Gateway call first: if it fails, nothing is recorded and books stay consistent.
        return await plugin.RefundAsync(invoice.GatewayOrderId, (long)(refundAmount * 100), ct);
    }
}
