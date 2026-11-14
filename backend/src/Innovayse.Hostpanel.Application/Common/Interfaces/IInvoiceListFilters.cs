using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The invoice filters every invoice list shares: the status and the creation period, as calendar days (UTC). A list query that offers them
/// implements this next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.InvoiceListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.InvoiceFilterExtensions"/>) are written once.
/// </summary>
public interface IInvoiceListFilters
{
    /// <summary>Gets the lifecycle status to keep; null keeps every status.</summary>
    public InvoiceStatus? Status { get; }

    /// <summary>Gets the first creation day to keep, inclusive; null for no lower bound.</summary>
    public DateOnly? From { get; }

    /// <summary>Gets the last creation day to keep, the whole day included; null for no upper bound.</summary>
    public DateOnly? To { get; }
}
