namespace Innovayse.Application.Billing.Commands.DeleteInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Permanently deletes a draft or cancelled invoice.</summary>
public sealed class DeleteInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the deletion is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Draft or Cancelled status.</exception>
    public async Task HandleAsync(DeleteInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        if (invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.Cancelled))
        {
            throw new InvalidOperationException(
                $"Only Draft or Cancelled invoices can be deleted; current status is {invoice.Status}.");
        }

        repo.Remove(invoice);
        await uow.SaveChangesAsync(ct);
    }
}
