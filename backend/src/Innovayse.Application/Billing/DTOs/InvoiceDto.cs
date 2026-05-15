namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO representing a full invoice with its line items.</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="PaidAt">Payment timestamp (UTC); null when unpaid.</param>
/// <param name="Total">Sum of all line item amounts.</param>
/// <param name="GatewayTransactionId">Payment gateway reference; null when unpaid.</param>
/// <param name="Items">Line items on the invoice.</param>
public record InvoiceDto(
    int Id,
    int ClientId,
    InvoiceStatus Status,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Total,
    string? GatewayTransactionId,
    IReadOnlyList<InvoiceItemDto> Items);
