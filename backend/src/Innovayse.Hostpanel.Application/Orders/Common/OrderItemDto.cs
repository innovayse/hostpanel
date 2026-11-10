using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>DTO representing a single line item within an order.</summary>
/// <param name="Id">Order item primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Product name snapshot at order time.</param>
/// <param name="BillingCycle">Selected billing cycle.</param>
/// <param name="Domain">Optional domain name for hosting products.</param>
/// <param name="Hostname">Optional hostname for VPS/server products.</param>
/// <param name="FirstPaymentAmount">First payment amount snapshot at order time.</param>
/// <param name="RecurringAmount">Recurring charge amount snapshot at order time.</param>
/// <param name="Status">Current lifecycle status of this item.</param>
/// <param name="FulfilmentState">Where fulfilling the line stands: pending, in progress, fulfilled, failed, refunding, partly refunded or refunded.</param>
/// <param name="FulfilmentErrorCode">The code the last failed attempt ended with, or null when the line did not fail or gave none.</param>
/// <param name="FulfilmentAttempts">How many times fulfilling the line has been attempted, the first run included.</param>
/// <param name="LastFulfilmentAttemptAt">When the last attempt started or ended, or null before the first one.</param>
/// <param name="CanRetry">
/// Whether staff may retry the line now, decided by the server with the configured retry lease
/// (<c>Orders:RetryLeaseMinutes</c>), so the admin never works it out with a lease of its own.
/// </param>
/// <param name="CanClose">Whether staff may close the line as refunded by hand: it failed, its refund is stuck, or it was only partly refunded.</param>
public sealed record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    BillingCycle BillingCycle,
    string? Domain,
    string? Hostname,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    OrderItemStatus Status,
    OrderItemFulfilmentState FulfilmentState,
    string? FulfilmentErrorCode,
    int FulfilmentAttempts,
    DateTimeOffset? LastFulfilmentAttemptAt,
    bool CanRetry,
    bool CanClose);
