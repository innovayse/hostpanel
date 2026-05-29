namespace Innovayse.Application.Billing.Commands.CancelInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Cancels an invoice so it will not be collected.</summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class CancelInvoiceHandler(
    IInvoiceRepository repo,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
{
    /// <summary>
    /// Handles <see cref="CancelInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The cancel command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the cancellation is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invoice cannot be cancelled (e.g., already paid).</exception>
    public async Task HandleAsync(CancelInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.Cancel();
        activityLogRepo.Add(ActivityLog.Create(
            invoice.ClientId,
            $"Cancelled Invoice - Invoice ID: {invoice.Id}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));
        await uow.SaveChangesAsync(ct);
    }
}
