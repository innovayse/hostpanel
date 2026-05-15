namespace Innovayse.Application.Billing.Commands.MarkInvoiceOverdue;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Marks an unpaid invoice as overdue (called by a scheduled job or admin).</summary>
public sealed class MarkInvoiceOverdueHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="MarkInvoiceOverdueCommand"/>.
    /// </summary>
    /// <param name="cmd">The mark-overdue command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the status change is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found or is in a non-transitionable status (Paid or Cancelled).</exception>
    public async Task HandleAsync(MarkInvoiceOverdueCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.MarkOverdue();
        await uow.SaveChangesAsync(ct);
    }
}
