namespace Innovayse.Application.Billing.Commands.InvoiceSelectedItems;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Creates an invoice from selected uninvoiced billable items and marks them as invoiced.
/// </summary>
public sealed class InvoiceSelectedItemsHandler(
    IBillableItemRepository billableItemRepo,
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="InvoiceSelectedItemsCommand"/>.
    /// </summary>
    /// <param name="cmd">The invoice selected items command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created invoice ID.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when any billable item does not belong to the specified client or is already invoiced.
    /// </exception>
    public async Task<int> HandleAsync(InvoiceSelectedItemsCommand cmd, CancellationToken ct)
    {
        var items = await billableItemRepo.FindByIdsAsync(cmd.BillableItemIds, ct);

        if (items.Count != cmd.BillableItemIds.Count)
        {
            throw new InvalidOperationException("One or more billable item IDs were not found.");
        }

        foreach (var item in items)
        {
            if (item.ClientId != cmd.ClientId)
            {
                throw new InvalidOperationException(
                    $"Billable item {item.Id} does not belong to client {cmd.ClientId}.");
            }

            if (item.InvoiceId is not null)
            {
                throw new InvalidOperationException(
                    $"Billable item {item.Id} is already invoiced on invoice {item.InvoiceId}.");
            }
        }

        var invoice = Invoice.Create(cmd.ClientId, DateTimeOffset.UtcNow.AddDays(14));

        foreach (var item in items)
        {
            invoice.AddItem(item.Description, item.Amount, 1);
        }

        invoiceRepo.Add(invoice);
        await uow.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            item.MarkInvoiced(invoice.Id);
        }

        await uow.SaveChangesAsync(ct);
        return invoice.Id;
    }
}
