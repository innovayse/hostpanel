using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one way an invoice list narrows its invoices: the search text on the invoice id and the payment method, the status
/// and the creation period, all in SQL ahead of ordering and paging, so the staff list and the client's list cannot differ.
/// </summary>
public static class InvoiceFilterExtensions
{
    /// <summary>Keeps the invoices the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The invoice list query.</typeparam>
    /// <param name="invoices">The invoices to narrow.</param>
    /// <param name="query">The search text and the invoice filters.</param>
    /// <returns>The narrowed invoices.</returns>
    public static IQueryable<Invoice> Matching<T>(this IQueryable<Invoice> invoices, T query)
        where T : IListQuery, IInvoiceListFilters
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(query);

        invoices = invoices.SearchIncludingId(query.Search, i => i.Id, i => i.PaymentMethod);

        if (query.Status is { } status)
        {
            invoices = invoices.Where(i => i.Status == status);
        }

        return invoices.InPeriod(i => i.CreatedAt, query.From, query.To);
    }
}
