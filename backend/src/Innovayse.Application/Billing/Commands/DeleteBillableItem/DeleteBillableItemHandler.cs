namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Deletes an uninvoiced billable item.
/// </summary>
public sealed class DeleteBillableItemHandler(IBillableItemRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteBillableItemCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete billable item command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the billable item is not found or is already invoiced.
    /// </exception>
    public async Task HandleAsync(DeleteBillableItemCommand cmd, CancellationToken ct)
    {
        var item = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Billable item {cmd.Id} not found.");

        if (item.InvoiceId is not null)
        {
            throw new InvalidOperationException(
                $"Cannot delete billable item {cmd.Id} because it is already invoiced on invoice {item.InvoiceId}.");
        }

        repo.Remove(item);
        await uow.SaveChangesAsync(ct);
    }
}
