using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The billable item filter every billable item list shares: the type. A list query that offers it implements this next to
/// <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.BillableItemListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.BillableItemFilterExtensions"/>) are written once.
/// </summary>
public interface IBillableItemListFilters
{
    /// <summary>Gets the type to keep; null keeps every type.</summary>
    public BillableItemType? Type { get; }
}
