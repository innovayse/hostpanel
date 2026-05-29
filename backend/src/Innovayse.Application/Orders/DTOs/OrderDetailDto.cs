namespace Innovayse.Application.Orders.DTOs;

/// <summary>DTO containing full order details including line items.</summary>
/// <param name="Id">Order primary key.</param>
/// <param name="OrderNumber">Human-readable order number (e.g. "ORD-0001").</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the client (first + last).</param>
/// <param name="Status">Current lifecycle status as a string.</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Total">Sum of all item first-payment amounts.</param>
/// <param name="InvoiceId">FK to the linked invoice, or null if not yet created.</param>
/// <param name="InvoiceStatus">Status of the linked invoice, or null if no invoice.</param>
/// <param name="IpAddress">Client's IP address at checkout time.</param>
/// <param name="Notes">Admin notes attached to the order.</param>
/// <param name="Items">Line items in the order.</param>
/// <param name="CreatedAt">UTC timestamp when the order was placed.</param>
public record OrderDetailDto(
    int Id,
    string OrderNumber,
    int ClientId,
    string ClientName,
    string Status,
    string PaymentMethod,
    decimal Total,
    int? InvoiceId,
    string? InvoiceStatus,
    string? IpAddress,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedAt);
