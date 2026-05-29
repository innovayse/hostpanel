namespace Innovayse.Application.Billing.Commands.CreateTimeBillingEntries;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Creates billable items from time billing entries and persists them.
/// </summary>
public sealed class CreateTimeBillingEntriesHandler(IBillableItemRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateTimeBillingEntriesCommand"/>.
    /// </summary>
    /// <param name="cmd">The create time billing entries command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of newly created billable item IDs.</returns>
    public async Task<IReadOnlyList<int>> HandleAsync(CreateTimeBillingEntriesCommand cmd, CancellationToken ct)
    {
        var items = cmd.Entries.Select(entry => BillableItem.Create(
            cmd.ClientId,
            entry.Description,
            entry.Hours * entry.Rate,
            "USD",
            BillableItemType.OneTime)).ToList();

        repo.AddRange(items);
        await uow.SaveChangesAsync(ct);
        return items.Select(x => x.Id).ToList();
    }
}
