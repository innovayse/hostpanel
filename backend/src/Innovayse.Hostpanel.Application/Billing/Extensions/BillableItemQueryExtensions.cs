using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one definition of "due for automatic invoicing" for billable items, shared by the daily run
/// that lists the clients to invoice and the per-client use case that invoices them, so the two can
/// never disagree on what is due.
/// </summary>
public static class BillableItemQueryExtensions
{
    /// <summary>Narrows to the items of one type whose invoicing moment has arrived.</summary>
    /// <param name="items">The billable items.</param>
    /// <param name="type">Whether to select the one-time items or the recurring ones.</param>
    /// <param name="now">The moment of the run.</param>
    /// <returns>
    /// One-time items not yet invoiced whose due date has passed, or recurring items whose next due
    /// date has passed (a recurring item is re-armed after each invoice, so it carries no invoiced flag).
    /// </returns>
    public static IQueryable<BillableItem> DueForInvoicing(
        this IQueryable<BillableItem> items, BillableItemType type, DateTimeOffset now) =>
        type == BillableItemType.OneTime
            ? items.Where(x => !x.IsInvoiced && x.Type == BillableItemType.OneTime && x.NextDueDate <= now)
            : items.Where(x => x.Type == BillableItemType.Recurring && x.NextDueDate != null && x.NextDueDate <= now);
}
