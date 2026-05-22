namespace Innovayse.Application.Billing.Queries.GetMyInvoices;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns all invoices for the authenticated client, newest first.</summary>
public sealed class GetMyInvoicesHandler(IInvoiceRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetMyInvoicesQuery"/>.
    /// </summary>
    /// <param name="query">The get my invoices query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices for the client with their line items and transactions, ordered newest first.</returns>
    public async Task<IReadOnlyList<InvoiceDto>> HandleAsync(GetMyInvoicesQuery query, CancellationToken ct)
    {
        var invoices = await repo.ListByClientAsync(query.ClientId, ct);

        return invoices.Select(inv => GetInvoiceHandler.MapToDto(inv)).ToList();
    }
}
