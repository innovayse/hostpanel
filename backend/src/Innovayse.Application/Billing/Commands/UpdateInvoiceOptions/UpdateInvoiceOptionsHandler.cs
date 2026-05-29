namespace Innovayse.Application.Billing.Commands.UpdateInvoiceOptions;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Updates invoice options (dates, payment method, tax rate).</summary>
public sealed class UpdateInvoiceOptionsHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateInvoiceOptionsCommand"/>.
    /// </summary>
    /// <param name="cmd">The update options command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when changes are persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task HandleAsync(UpdateInvoiceOptionsCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.UpdateOptions(cmd.InvoiceDate, cmd.DueDate, cmd.PaymentMethod, cmd.TaxRate);
        await uow.SaveChangesAsync(ct);
    }
}
