namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO representing a full invoice with its line items and transactions.</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="PaidAt">Payment timestamp (UTC); null when unpaid.</param>
/// <param name="Total">Final total after tax and credit.</param>
/// <param name="SubTotal">Sum of all line item amounts before tax and credit.</param>
/// <param name="Tax">Computed tax amount.</param>
/// <param name="TaxRate">Tax rate percentage.</param>
/// <param name="Credit">Total credit applied.</param>
/// <param name="GatewayTransactionId">Payment gateway reference; null when unpaid.</param>
/// <param name="Notes">Optional invoice notes.</param>
/// <param name="InvoiceDate">Invoice issue date (UTC).</param>
/// <param name="PaymentMethod">Preferred payment method; null when not specified.</param>
/// <param name="ClientName">Display name of the owning client.</param>
/// <param name="Items">Line items on the invoice.</param>
/// <param name="Transactions">Financial transactions recorded against the invoice.</param>
public record InvoiceDto(
    int Id,
    int ClientId,
    InvoiceStatus Status,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Total,
    decimal SubTotal,
    decimal Tax,
    decimal TaxRate,
    decimal Credit,
    string? GatewayTransactionId,
    string? Notes,
    DateTimeOffset InvoiceDate,
    string? PaymentMethod,
    string ClientName,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<InvoiceTransactionDto> Transactions);
