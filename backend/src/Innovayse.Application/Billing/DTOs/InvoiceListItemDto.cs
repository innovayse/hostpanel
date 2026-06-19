namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Domain.Billing;

/// <summary>DTO for a single row in paginated invoice lists (no line items).</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="Total">Grand total including tax minus credit.</param>
/// <param name="SubTotal">Sum of all line item amounts before tax.</param>
/// <param name="Tax">Calculated tax amount.</param>
/// <param name="InvoiceDate">Invoice issue date (UTC).</param>
/// <param name="PaidAt">Payment timestamp, null if unpaid.</param>
/// <param name="PaymentMethod">Configured payment method label.</param>
public record InvoiceListItemDto(
    int Id,
    int ClientId,
    string ClientName,
    InvoiceStatus Status,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    decimal Total,
    decimal SubTotal,
    decimal Tax,
    DateTimeOffset InvoiceDate,
    DateTimeOffset? PaidAt,
    string? PaymentMethod);
