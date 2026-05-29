namespace Innovayse.Application.Billing.Commands.DeleteInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Permanently deletes an invoice regardless of its current status.</summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class DeleteInvoiceHandler(
    IInvoiceRepository repo,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
{
    /// <summary>
    /// Handles <see cref="DeleteInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the deletion is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(DeleteInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        var clientId = invoice.ClientId;
        var invoiceId = invoice.Id;

        repo.Remove(invoice);
        activityLogRepo.Add(ActivityLog.Create(
            clientId,
            $"Deleted Invoice - Invoice ID: {invoiceId}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));
        await uow.SaveChangesAsync(ct);
    }
}
