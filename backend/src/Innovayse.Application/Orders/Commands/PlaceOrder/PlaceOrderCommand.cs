namespace Innovayse.Application.Orders.Commands.PlaceOrder;

/// <summary>
/// Command to place a new order. Supports both authenticated clients and guest checkout
/// (by providing registration details).
/// </summary>
/// <remarks>
/// <para>
/// Carries no client id. A signed-in caller orders for their own account, resolved inside the
/// handler from the credential; a caller with no credential registers as part of checking out.
/// An id here would let anyone place an order — and the invoice that comes with it — against
/// somebody else's account.
/// </para>
/// <para>
/// The IP recorded on the order is likewise the handler's to read, not the caller's to state:
/// a value sent in the body is worth nothing as a record of where a checkout came from.
/// </para>
/// </remarks>
/// <param name="FirstName">Guest's first name (required for guest checkout).</param>
/// <param name="LastName">Guest's last name (required for guest checkout).</param>
/// <param name="Email">Guest's email address (required for guest checkout).</param>
/// <param name="Password">Guest's password (required for guest checkout).</param>
/// <param name="Phone">Guest's phone number (optional).</param>
/// <param name="PaymentMethod">Payment gateway module name selected at checkout.</param>
/// <param name="Items">One or more products to include in the order.</param>
public record PlaceOrderCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? Phone,
    string PaymentMethod,
    IReadOnlyList<PlaceOrderItemDto> Items);

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
