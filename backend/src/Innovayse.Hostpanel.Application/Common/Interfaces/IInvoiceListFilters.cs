using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The invoice filters every invoice list shares: the status and the creation period. A list query that offers them
/// implements this next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.InvoiceListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.InvoiceFilterExtensions"/>) are written once.
/// </summary>
public interface IInvoiceListFilters
{
    /// <summary>Gets the lifecycle status to keep; null keeps every status.</summary>
    public InvoiceStatus? Status { get; }

    /// <summary>Gets the first creation moment to keep, inclusive; null for no lower bound.</summary>
    public DateTimeOffset? From { get; }

    /// <summary>Gets any moment of the last creation day to keep (UTC), the whole day included; null for no upper bound.</summary>
    public DateTimeOffset? To { get; }
}
