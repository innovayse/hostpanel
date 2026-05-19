namespace Innovayse.Application.Billing.Commands.RefundInvoicePayment;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Records a refund against a paid invoice.
/// Supports three refund types: Gateway (via provider), Manual (external), and CreditBalance (adds to client credit).
/// </summary>
public sealed class RefundInvoicePaymentHandler(IInvoiceRepository repo, IUnitOfWork uow)
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
            _ => $"gateway-refund-{cmd.InvoiceId}-{DateTimeOffset.UtcNow.Ticks}",
        };

        var gateway = cmd.RefundType == "CreditBalance" ? "Credit Balance" : cmd.Gateway;

        invoice.AddRefund(DateTimeOffset.UtcNow, gateway, txnRef, refundAmount, 0m, cmd.Notes);
        await uow.SaveChangesAsync(ct);
    }
}
