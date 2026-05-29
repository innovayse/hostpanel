namespace Innovayse.Application.Billing.Commands.RemoveInvoiceCredit;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Removes a specific credit amount from an invoice.</summary>
public sealed class RemoveInvoiceCreditHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="RemoveInvoiceCreditCommand"/>.
    /// </summary>
    /// <param name="cmd">The remove credit command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the credit is removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(RemoveInvoiceCreditCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.RemoveCredit(cmd.Amount);
        await uow.SaveChangesAsync(ct);
    }
}
