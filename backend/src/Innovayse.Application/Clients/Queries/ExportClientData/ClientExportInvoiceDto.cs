namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Billing;

/// <summary>One invoice, as the <c>invoices</c> section of a client data export lists it.</summary>
/// <param name="Id">The invoice's primary key, which is also its human-facing number.</param>
/// <param name="Status">Invoice status — Draft, Unpaid, Paid, Overdue, Cancelled or Refunded.</param>
/// <param name="Total">Gross amount payable, in the invoice's currency.</param>
/// <param name="CreatedAt">When the invoice was raised.</param>
/// <param name="DueDate">When payment falls due.</param>
/// <param name="PaidAt">When it was settled, or null while it is still outstanding.</param>
public sealed record ClientExportInvoiceDto(
    int Id,
    InvoiceStatus Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueDate,
    DateTimeOffset? PaidAt);
