namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Creates a new billable item and persists it.
/// </summary>
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

        repo.Add(item);
        await uow.SaveChangesAsync(ct);
        return item.Id;
    }
}
