using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Maps a domain <see cref="Invoice"/> to <see cref="InvoiceDto"/> (the admin read, <c>GetInvoiceHandler</c>)
/// and to <see cref="MyInvoiceDto"/> (the client-facing reads, <c>GetMyInvoiceHandler</c>,
/// <c>GetMyInvoicesHandler</c> and <c>GetMyServiceInvoicesHandler</c>), so a field added to either
/// DTO is added once rather than in several places that quietly drift apart.
/// </summary>
public static class InvoiceMapper
{
    /// <summary>Maps a domain invoice to a full DTO.</summary>
    /// <param name="invoice">The domain invoice.</param>
    /// <param name="clientName">Display name of the owning client.</param>
    /// <returns>The mapped DTO.</returns>
    public static InvoiceDto MapToDto(Invoice invoice, string clientName) =>
        new(
            invoice.Id,
            invoice.ClientId,
            clientName,
            invoice.Status,
            invoice.Currency,
            new InvoiceDatesDto(invoice.InvoiceDate, invoice.DueDate, invoice.CreatedAt, invoice.PaidAt),
            new InvoiceAmountsDto(invoice.SubTotal, invoice.Credit, invoice.Tax, invoice.TaxRate, invoice.Total),
            invoice.AmountDue,
            new InvoicePaymentDto(invoice.PaymentMethod, invoice.GatewayTransactionId),
            invoice.Notes,
            invoice.Items.Select(ToItemDto).ToList(),
            invoice.Transactions.Select(t => new InvoiceTransactionDto(
                t.Id, t.Date, t.Gateway, t.TransactionId, t.Amount, t.Fees, t.Type, t.Notes))
                         .ToList());

    /// <summary>
    /// Maps a domain invoice to the client's own <see cref="MyInvoiceDto"/>, which carries no
    /// gateway reference, fee or staff note.
    /// </summary>
    /// <param name="invoice">The domain invoice, with its items and transactions loaded.</param>
    /// <returns>The mapped DTO.</returns>
    public static MyInvoiceDto MapToMyDto(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Status,
            invoice.Currency,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.CreatedAt,
            invoice.PaidAt,
            invoice.Total,
            invoice.SubTotal,
            invoice.Tax,
            invoice.TaxRate,
            invoice.Credit,
            invoice.AmountDue,
            invoice.PaymentMethod,
            invoice.Items.Select(ToItemDto).ToList(),
            invoice.Transactions.Select(t => new MyInvoiceTransactionDto(t.Id, t.Date, t.Amount, t.Type)).ToList());

    /// <summary>Maps one invoice line to <see cref="InvoiceItemDto"/>.</summary>
    /// <param name="item">The invoice line.</param>
    /// <returns>The mapped DTO.</returns>
    private static InvoiceItemDto ToItemDto(InvoiceItem item) =>
        new(item.Id, item.Description, item.UnitPrice, item.Quantity, item.Amount, item.ProductSnapshot?.Sku);
}
