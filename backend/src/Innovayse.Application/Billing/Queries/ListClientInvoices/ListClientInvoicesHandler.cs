namespace Innovayse.Application.Billing.Queries.ListClientInvoices;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a paginated, filtered list of invoices for a specific client.</summary>
public sealed class ListClientInvoicesHandler(IInvoiceRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="ListClientInvoicesQuery"/>.
    /// </summary>
    /// <param name="query">The list client invoices query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of invoice list items for the client.</returns>
    public async Task<PagedResult<InvoiceListItemDto>> HandleAsync(ListClientInvoicesQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListByClientAsync(
            query.ClientId, page, pageSize, query.Status, query.From, query.To, ct);

        var clients = await clientRepo.FindByIdsAsync([query.ClientId], ct);
        var client = clients.FirstOrDefault();
        var clientName = client is not null ? $"{client.FirstName} {client.LastName}".Trim() : $"Client #{query.ClientId}";

        var dtos = items.Select(inv => new InvoiceListItemDto(
            inv.Id, inv.ClientId, clientName, inv.Status, inv.DueDate, inv.CreatedAt, inv.Total, inv.SubTotal, inv.Tax, inv.InvoiceDate, inv.PaidAt, inv.PaymentMethod))
            .ToList();

        return new PagedResult<InvoiceListItemDto>(dtos, total, page, pageSize);
    }
}
