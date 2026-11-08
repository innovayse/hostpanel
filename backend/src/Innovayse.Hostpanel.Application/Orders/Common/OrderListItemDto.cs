using Innovayse.Hostpanel.Domain.Orders.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>DTO for order list items in the admin view.</summary>
/// <param name="Id">Order primary key.</param>
/// <param name="OrderNumber">Human-readable order number (e.g. "ORD-0001").</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the client (first + last); every order has one, since its client cannot be deleted.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Total">Sum of all item first-payment amounts.</param>
/// <param name="Currency">ISO 4217 code the order's amounts are in.</param>
/// <param name="InvoiceId">FK to the linked invoice, or null if not yet created.</param>
/// <param name="ItemCount">Number of line items in the order.</param>
/// <param name="FailedLineCount">Number of lines that failed to be fulfilled and still need staff: failed lines that can be retried, and partly refunded lines whose refund staff must finish by hand.</param>
/// <param name="CreatedAt">UTC timestamp when the order was placed.</param>
public sealed record OrderListItemDto(
    int Id,
    string OrderNumber,
    int ClientId,
    string ClientName,
    OrderStatus Status,
    string PaymentMethod,
    decimal Total,
    string Currency,
    int? InvoiceId,
    int ItemCount,
    int FailedLineCount,
    DateTimeOffset CreatedAt);
