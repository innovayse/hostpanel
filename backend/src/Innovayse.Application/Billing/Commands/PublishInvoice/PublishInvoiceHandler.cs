namespace Innovayse.Application.Billing.Commands.PublishInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Publishes a draft invoice so it becomes payable.</summary>
public sealed class PublishInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="PublishInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The publish command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the publish is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Draft status.</exception>
    public async Task HandleAsync(PublishInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        invoice.Publish();
        await uow.SaveChangesAsync(ct);
    }
}
