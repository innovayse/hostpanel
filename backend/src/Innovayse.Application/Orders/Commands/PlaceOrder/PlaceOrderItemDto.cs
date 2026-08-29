namespace Innovayse.Application.Orders.Commands.PlaceOrder;

/// <summary>DTO representing a single item in a place-order request.</summary>
/// <param name="ProductId">FK to the product being ordered.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Domain">Optional domain name for hosting or domain registration items.</param>
/// <param name="Hostname">Optional hostname for VPS/server products.</param>
/// <param name="DomainAction">Domain action: "register" or "transfer". Null for hosting items.</param>
/// <param name="EppCode">EPP authorization code for domain transfers.</param>
/// <param name="Years">Domain registration/transfer period in years (1–10).</param>
public record PlaceOrderItemDto(
    int ProductId,
    string BillingCycle,
    string? Domain,
    string? Hostname,
    string? DomainAction = null,
    string? EppCode = null,
    int? Years = null);
