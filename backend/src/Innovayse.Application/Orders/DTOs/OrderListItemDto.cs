namespace Innovayse.Application.Orders.DTOs;

/// <summary>DTO for order list items in the admin view.</summary>
/// <param name="Id">Order primary key.</param>
/// <param name="OrderNumber">Human-readable order number (e.g. "ORD-0001").</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the client (first + last).</param>
/// <param name="Status">Current lifecycle status as a string.</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Total">Sum of all item first-payment amounts.</param>
/// <param name="InvoiceId">FK to the linked invoice, or null if not yet created.</param>
/// <param name="ItemCount">Number of line items in the order.</param>
/// <param name="CreatedAt">UTC timestamp when the order was placed.</param>
public record OrderListItemDto(
    int Id,
    string OrderNumber,
    int ClientId,
    string ClientName,
    string Status,
    string PaymentMethod,
    decimal Total,
    int? InvoiceId,
    int ItemCount,
    DateTimeOffset CreatedAt);
