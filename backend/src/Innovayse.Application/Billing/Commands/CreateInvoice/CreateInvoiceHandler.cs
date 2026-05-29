namespace Innovayse.Application.Billing.Commands.CreateInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Creates a new invoice with the provided line items and persists it.
/// </summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="activityLogRepo">Activity log repository for audit trail.</param>
/// <param name="ctx">Current request context providing admin identity and IP.</param>
public sealed class CreateInvoiceHandler(
    IInvoiceRepository repo,
    IUnitOfWork uow,
    IActivityLogRepository activityLogRepo,
    ICurrentRequestContext ctx)
{
    /// <summary>
    /// Handles <see cref="CreateInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The create invoice command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created invoice ID.</returns>
    /// <exception cref="InvalidOperationException">Propagated from domain when invoice invariants are violated (e.g., invalid item price or quantity).</exception>
    public async Task<int> HandleAsync(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = Invoice.Create(cmd.ClientId, cmd.DueDate, cmd.IsDraft);

        foreach (var item in cmd.Items)
        {
            invoice.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }

        repo.Add(invoice);
        await uow.SaveChangesAsync(ct);

        activityLogRepo.Add(ActivityLog.Create(
            cmd.ClientId,
            $"Created Manual Invoice - Invoice ID: {invoice.Id}",
            ctx.AdminId, ctx.AdminName, ctx.AdminEmail, ctx.IpAddress));
        await uow.SaveChangesAsync(ct);

        return invoice.Id;
    }
}
