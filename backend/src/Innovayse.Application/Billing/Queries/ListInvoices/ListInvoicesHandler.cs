namespace Innovayse.Application.Billing.Queries.ListInvoices;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a paginated, optionally filtered list of all invoices for admin consumption.</summary>
public sealed class ListInvoicesHandler(IInvoiceRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListInvoicesQuery"/>.
    /// </summary>
    /// <param name="query">The list invoices query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of invoice list items.</returns>
    public async Task<PagedResult<InvoiceListItemDto>> HandleAsync(ListInvoicesQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        InvoiceStatus? status = null;
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<InvoiceStatus>(query.Status, out var parsed))
        {
            status = parsed;
        }

        var (items, total) = await repo.ListAsync(page, pageSize, status, query.From, query.To, ct);

        var dtos = items.Select(inv => new InvoiceListItemDto(
            inv.Id, inv.ClientId, inv.Status, inv.DueDate, inv.CreatedAt, inv.Total, inv.SubTotal, inv.Tax, inv.InvoiceDate, inv.PaidAt, inv.PaymentMethod))
            .ToList();

        return new PagedResult<InvoiceListItemDto>(dtos, total, page, pageSize);
    }
}
