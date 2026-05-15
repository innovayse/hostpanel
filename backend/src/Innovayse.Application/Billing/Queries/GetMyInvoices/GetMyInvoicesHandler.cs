namespace Innovayse.Application.Billing.Queries.GetMyInvoices;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns all invoices for the authenticated client, newest first.</summary>
public sealed class GetMyInvoicesHandler(IInvoiceRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetMyInvoicesQuery"/>.
    /// </summary>
    /// <param name="query">The get my invoices query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices for the client with their line items, ordered newest first.</returns>
    public async Task<IReadOnlyList<InvoiceDto>> HandleAsync(GetMyInvoicesQuery query, CancellationToken ct)
    {
        var invoices = await repo.ListByClientAsync(query.ClientId, ct);

        return invoices.Select(inv => new InvoiceDto(
            inv.Id,
            inv.ClientId,
            inv.Status,
            inv.DueDate,
            inv.CreatedAt,
            inv.PaidAt,
            inv.Total,
            inv.GatewayTransactionId,
            inv.Items.Select(i => new InvoiceItemDto(i.Id, i.Description, i.UnitPrice, i.Quantity, i.Amount))
                     .ToList()))
            .ToList();
    }
}
