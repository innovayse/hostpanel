namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Deletes a billable item.</summary>
public sealed class DeleteBillableItemHandler(IBillableItemRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteBillableItemCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete billable item command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found.</exception>
    public async Task HandleAsync(DeleteBillableItemCommand cmd, CancellationToken ct)
    {
        var item = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Billable item {cmd.Id} not found.");

        repo.Delete(item);
        await uow.SaveChangesAsync(ct);
    }
}
