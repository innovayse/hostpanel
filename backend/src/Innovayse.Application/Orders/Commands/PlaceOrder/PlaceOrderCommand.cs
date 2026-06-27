namespace Innovayse.Application.Orders.Commands.PlaceOrder;

/// <summary>
/// Command to place a new order. Supports both authenticated clients (by <see cref="ClientId"/>)
/// and guest checkout (by providing registration details).
/// </summary>
/// <param name="ClientId">Existing client ID, or null for guest checkout.</param>
/// <param name="FirstName">Guest's first name (required when <see cref="ClientId"/> is null).</param>
/// <param name="LastName">Guest's last name (required when <see cref="ClientId"/> is null).</param>
/// <param name="Email">Guest's email address (required when <see cref="ClientId"/> is null).</param>
/// <param name="Password">Guest's password (required when <see cref="ClientId"/> is null).</param>
/// <param name="Phone">Guest's phone number (optional).</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Items">One or more products to include in the order.</param>
/// <param name="IpAddress">Client's IP address at checkout time.</param>
public record PlaceOrderCommand(
    int? ClientId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? Phone,
    string PaymentMethod,
    IReadOnlyList<PlaceOrderItemDto> Items,
    string? IpAddress);

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
