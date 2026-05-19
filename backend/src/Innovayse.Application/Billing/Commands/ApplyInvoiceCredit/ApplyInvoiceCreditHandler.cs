namespace Innovayse.Application.Billing.Commands.ApplyInvoiceCredit;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Applies a credit to an invoice, reducing the amount owed.</summary>
public sealed class ApplyInvoiceCreditHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="ApplyInvoiceCreditCommand"/>.
    /// </summary>
    /// <param name="cmd">The apply credit command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the credit is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(ApplyInvoiceCreditCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.ApplyCredit(cmd.Amount);
        await uow.SaveChangesAsync(ct);
    }
}
