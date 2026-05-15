namespace Innovayse.Application.Billing.Commands.CancelInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Cancels an invoice so it will not be collected.</summary>
public sealed class CancelInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
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
        await uow.SaveChangesAsync(ct);
    }
}
