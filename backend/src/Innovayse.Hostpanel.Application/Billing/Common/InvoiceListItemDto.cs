using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;
/// <summary>DTO for a single row in paginated invoice lists (no line items).</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client; every invoice has one, since its client cannot be deleted.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="Total">Grand total including tax minus credit.</param>
/// <param name="SubTotal">Sum of all line item amounts before tax.</param>
/// <param name="Tax">Calculated tax amount.</param>
/// <param name="InvoiceDate">Invoice issue date (UTC).</param>
/// <param name="PaidAt">Payment timestamp, null if never paid, kept after a refund.</param>
/// <param name="PaymentMethod">Configured payment method label.</param>
/// <param name="Currency">ISO 4217 code of the currency the invoice is issued in, which the amounts above are in.</param>
public sealed record InvoiceListItemDto(
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
    string? PaymentMethod,
    string Currency);
