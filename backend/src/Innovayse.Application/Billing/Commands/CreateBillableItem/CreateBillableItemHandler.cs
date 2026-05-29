namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Creates a new billable item.</summary>
public sealed class CreateBillableItemHandler(IBillableItemRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateBillableItemCommand"/>.
    /// </summary>
    /// <param name="cmd">The create billable item command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created billable item ID.</returns>
    public async Task<int> HandleAsync(CreateBillableItemCommand cmd, CancellationToken ct)
    {
        var type = Enum.Parse<BillableItemType>(cmd.Type);

        var item = BillableItem.Create(
            cmd.ClientId,
            cmd.Description,
            cmd.Amount,
            cmd.Currency,
            type,
            cmd.RecurringPeriod,
            cmd.NextDueDate);

        repo.Add(item);
        await uow.SaveChangesAsync(ct);
        return item.Id;
    }
}
