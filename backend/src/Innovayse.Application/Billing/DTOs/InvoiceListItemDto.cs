namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a single row in paginated invoice lists (no line items).</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="Total">Sum of all line item amounts.</param>
public record InvoiceListItemDto(
    int Id,
    int ClientId,
    InvoiceStatus Status,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    decimal Total);
