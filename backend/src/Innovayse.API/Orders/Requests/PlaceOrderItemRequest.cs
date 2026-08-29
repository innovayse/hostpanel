namespace Innovayse.API.Orders.Requests;

/// <summary>A single item in the order request.</summary>
/// <param name="Pid">Product ID (WHMCS naming convention).</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Domain">Optional domain name for hosting or domain registration items.</param>
/// <param name="Hostname">Optional hostname for VPS/server products.</param>
/// <param name="DomainAction">Domain action: "register" or "transfer". Null for hosting items.</param>
/// <param name="EppCode">EPP authorization code for domain transfers.</param>
/// <param name="Years">Domain registration/transfer period in years (1–10).</param>
public record PlaceOrderItemRequest(
    int Pid,
    string BillingCycle,
    string? Domain,
    string? Hostname,
    string? DomainAction = null,
    string? EppCode = null,
    int? Years = null);
