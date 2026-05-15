namespace Innovayse.Application.Billing.Queries.GetInvoice;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a full <see cref="InvoiceDto"/> including line items.</summary>
public sealed class GetInvoiceHandler(IInvoiceRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetInvoiceQuery"/>.
    /// </summary>
    /// <param name="query">The get invoice query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice DTO with line items.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task<InvoiceDto> HandleAsync(GetInvoiceQuery query, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(query.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {query.InvoiceId} not found.");

        return new InvoiceDto(
            invoice.Id,
            invoice.ClientId,
            invoice.Status,
            invoice.DueDate,
            invoice.CreatedAt,
            invoice.PaidAt,
            invoice.Total,
            invoice.GatewayTransactionId,
            invoice.Items.Select(i => new InvoiceItemDto(i.Id, i.Description, i.UnitPrice, i.Quantity, i.Amount))
                         .ToList());
    }
}
