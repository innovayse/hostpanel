namespace Innovayse.Application.Billing.Commands.PayInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Charges the client via <see cref="IPaymentGateway"/> and marks the invoice as paid.
/// </summary>
public sealed class PayInvoiceHandler(
    IInvoiceRepository repo,
    IPaymentGateway gateway,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="PayInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The pay invoice command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the payment is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the payment gateway charge fails.</exception>
    public async Task HandleAsync(PayInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        var chargeRequest = new ChargeRequest(invoice.ClientId, invoice.Id, invoice.Total, cmd.Currency);
        var result = await gateway.ChargeAsync(chargeRequest, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Payment failed: {result.ErrorMessage}");
        }

        if (string.IsNullOrEmpty(result.TransactionId))
        {
            throw new InvalidOperationException("Payment gateway returned success but did not provide a transaction ID.");
        }

        invoice.MarkPaid(result.TransactionId);
        await uow.SaveChangesAsync(ct);
    }
}
