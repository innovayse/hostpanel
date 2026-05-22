namespace Innovayse.Application.Billing.Commands.UpdateInvoiceNotes;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Updates or clears the notes on an invoice.</summary>
public sealed class UpdateInvoiceNotesHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateInvoiceNotesCommand"/>.
    /// </summary>
    /// <param name="cmd">The update notes command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when changes are persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(UpdateInvoiceNotesCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.UpdateNotes(cmd.Notes);
        await uow.SaveChangesAsync(ct);
    }
}
