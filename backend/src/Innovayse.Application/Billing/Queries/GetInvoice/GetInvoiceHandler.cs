namespace Innovayse.Application.Billing.Queries.GetInvoice;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a full <see cref="InvoiceDto"/> including line items, transactions, and client name.</summary>
public sealed class GetInvoiceHandler(IInvoiceRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="GetInvoiceQuery"/>.
    /// </summary>
    /// <param name="query">The get invoice query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice DTO with line items, transactions, and client name.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    public async Task<InvoiceDto> HandleAsync(GetInvoiceQuery query, CancellationToken ct)
    {
        var invoice = await repo.FindByIdAsync(query.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {query.InvoiceId} not found.");

        var client = await clientRepo.FindByIdAsync(invoice.ClientId, ct);
        var clientName = client is not null
            ? $"{client.FirstName} {client.LastName}"
            : $"Client #{invoice.ClientId}";

        return MapToDto(invoice, clientName);
    }

    /// <summary>Maps a domain invoice to a full DTO.</summary>
    /// <param name="invoice">The domain invoice.</param>
    /// <param name="clientName">Display name of the owning client.</param>
    /// <returns>The mapped DTO.</returns>
    internal static InvoiceDto MapToDto(Invoice invoice, string clientName = "") =>
        new(
            invoice.Id,
            invoice.ClientId,
            invoice.Status,
            invoice.DueDate,
            invoice.CreatedAt,
            invoice.PaidAt,
            invoice.Total,
            invoice.SubTotal,
            invoice.Tax,
            invoice.TaxRate,
            invoice.Credit,
            invoice.GatewayTransactionId,
            invoice.Notes,
            invoice.InvoiceDate,
            invoice.PaymentMethod,
            clientName,
            invoice.Items.Select(i => new InvoiceItemDto(i.Id, i.Description, i.UnitPrice, i.Quantity, i.Amount))
                         .ToList(),
            invoice.Transactions.Select(t => new InvoiceTransactionDto(
                t.Id, t.Date, t.Gateway, t.TransactionId, t.Amount, t.Fees, t.Type, t.Notes))
                         .ToList());
}
