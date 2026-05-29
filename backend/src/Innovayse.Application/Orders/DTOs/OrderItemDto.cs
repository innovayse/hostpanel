namespace Innovayse.Application.Orders.DTOs;

/// <summary>DTO representing a single line item within an order.</summary>
/// <param name="Id">Order item primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Product name snapshot at order time.</param>
/// <param name="BillingCycle">Selected billing cycle ("monthly" or "annual").</param>
/// <param name="Domain">Optional domain name for hosting products.</param>
/// <param name="Hostname">Optional hostname for VPS/server products.</param>
/// <param name="FirstPaymentAmount">First payment amount snapshot at order time.</param>
/// <param name="RecurringAmount">Recurring charge amount snapshot at order time.</param>
/// <param name="Status">Current lifecycle status of this item.</param>
public record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string BillingCycle,
    string? Domain,
    string? Hostname,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string Status);
