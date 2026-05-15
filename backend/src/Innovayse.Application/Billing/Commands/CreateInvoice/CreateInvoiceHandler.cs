namespace Innovayse.Application.Billing.Commands.CreateInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Creates a new invoice with the provided line items and persists it.
/// </summary>
public sealed class CreateInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
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
        var invoice = Invoice.Create(cmd.ClientId, cmd.DueDate);

        foreach (var item in cmd.Items)
        {
            invoice.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }

        repo.Add(invoice);
        await uow.SaveChangesAsync(ct);
        return invoice.Id;
    }
}
