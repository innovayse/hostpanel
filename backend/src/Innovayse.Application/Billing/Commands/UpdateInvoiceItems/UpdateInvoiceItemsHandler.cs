namespace Innovayse.Application.Billing.Commands.UpdateInvoiceItems;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Applies item additions, updates, and deletions to an invoice.</summary>
public sealed class UpdateInvoiceItemsHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateInvoiceItemsCommand"/>.
    /// </summary>
    /// <param name="cmd">The update invoice items command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when changes are persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(UpdateInvoiceItemsCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        foreach (var entry in cmd.Items)
        {
            if (entry.Id is null && !entry.IsDeleted)
            {
                invoice.AddItem(entry.Description, entry.UnitPrice, entry.Quantity);
            }
            else if (entry.Id.HasValue && entry.IsDeleted)
            {
                invoice.RemoveItem(entry.Id.Value);
            }
            else if (entry.Id.HasValue && !entry.IsDeleted)
            {
                invoice.UpdateItem(entry.Id.Value, entry.Description, entry.UnitPrice, entry.Quantity);
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
