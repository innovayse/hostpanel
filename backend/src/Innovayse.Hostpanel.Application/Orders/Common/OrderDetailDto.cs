using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>DTO containing full order details including line items.</summary>
/// <param name="Id">Order primary key.</param>
/// <param name="OrderNumber">Human-readable order number (e.g. "ORD-0001").</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the client (first + last); every order has one, since its client cannot be deleted.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Total">
/// Sum of the line items' first-payment amounts, before tax and before any credit applied; what the
/// client actually owes is <paramref name="InvoiceTotal"/>.
/// </param>
/// <param name="Currency">ISO 4217 code the order's amounts are in.</param>
/// <param name="InvoiceId">FK to the linked invoice, or null if not yet created.</param>
/// <param name="InvoiceStatus">Status of the linked invoice, or null if no invoice.</param>
/// <param name="InvoiceTotal">
/// Total of the linked invoice, tax included, in the invoice currency, or null if no invoice.
/// </param>
/// <param name="IpAddress">Client's IP address at checkout time.</param>
/// <param name="Notes">Admin notes attached to the order.</param>
/// <param name="Items">Line items in the order.</param>
/// <param name="FraudScore">The fraud score (0 to 100) that held the order for review, or null when it was never held.</param>
/// <param name="FraudReasons">The reasons the fraud score came from; empty when the order was never held.</param>
/// <param name="CreatedAt">UTC timestamp when the order was placed.</param>
/// <param name="BundleId">FK to the product bundle the order was placed from, or null for an order of single products or once the bundle was deleted.</param>
/// <param name="BundleName">The name of that bundle, or null when <paramref name="BundleId"/> is.</param>
public sealed record OrderDetailDto(
    int Id,
    string OrderNumber,
    int ClientId,
    string ClientName,
    OrderStatus Status,
    string PaymentMethod,
    decimal Total,
    string Currency,
    int? InvoiceId,
    InvoiceStatus? InvoiceStatus,
    decimal? InvoiceTotal,
    string? IpAddress,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    int? FraudScore,
    IReadOnlyList<FraudReasonCode> FraudReasons,
    DateTimeOffset CreatedAt,
    int? BundleId = null,
    string? BundleName = null);
