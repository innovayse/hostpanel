namespace Innovayse.Application.Billing.Commands.DuplicateInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Creates a draft copy of an existing invoice.</summary>
public sealed class DuplicateInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DuplicateInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The duplicate command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created draft invoice.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the source invoice is not found.</exception>
    public async Task<int> HandleAsync(DuplicateInvoiceCommand cmd, CancellationToken ct)
    {
        var source = await repo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        var copy = source.Duplicate();
        repo.Add(copy);
        await uow.SaveChangesAsync(ct);
        return copy.Id;
    }
}
