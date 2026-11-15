using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The invoice filters every invoice list shares: the status and the creation period (<see cref="IPeriodListFilter"/>, calendar days, UTC).
/// A list query that offers them implements this next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.InvoiceListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.InvoiceFilterExtensions"/>) are written once.
/// </summary>
public interface IInvoiceListFilters : IPeriodListFilter
{
    /// <summary>Gets the lifecycle status to keep; null keeps every status.</summary>
    public InvoiceStatus? Status { get; }
}
