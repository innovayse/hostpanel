namespace Innovayse.Application.Billing.Commands.PayInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Charges the client via <see cref="IPaymentGateway"/> and marks the invoice as paid.
/// </summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="gateway">Payment gateway for charging the client.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class PayInvoiceHandler(
    IInvoiceRepository repo,
    IPaymentGateway gateway,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
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
        activityLogRepo.Add(ActivityLog.Create(
            invoice.ClientId,
            $"Invoice Marked Paid - Invoice ID: {invoice.Id}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));
        await uow.SaveChangesAsync(ct);
    }
}
