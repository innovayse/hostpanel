namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

<<<<<<< HEAD
/// <summary>Creates a new billable item.</summary>
=======
/// <summary>
/// Creates a new billable item and persists it.
/// </summary>
>>>>>>> origin/main
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
<<<<<<< HEAD
        var type = Enum.Parse<BillableItemType>(cmd.Type);

        var item = BillableItem.Create(
            cmd.ClientId,
            cmd.Description,
            cmd.Amount,
            cmd.Currency,
            type,
            cmd.RecurringPeriod,
            cmd.NextDueDate);
=======
        var item = BillableItem.Create(
            cmd.ClientId,
            cmd.ServiceId,
            cmd.Description,
            cmd.Amount,
            cmd.HoursQty,
            cmd.IsHours,
            cmd.InvoiceAction,
            cmd.DueDate,
            cmd.RecurrenceInterval,
            cmd.RecurrencePeriod,
            cmd.RecurrenceLimit);
>>>>>>> origin/main

        repo.Add(item);
        await uow.SaveChangesAsync(ct);
        return item.Id;
    }
}
