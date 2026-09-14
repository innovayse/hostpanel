namespace Innovayse.API.Orders.Requests;

/// <summary>Request body for placing a new order at checkout.</summary>
/// <param name="Items">One or more products to order.</param>
/// <param name="PaymentMethod">Payment gateway module name.</param>
/// <param name="FirstName">Guest first name (required for guest checkout).</param>
/// <param name="LastName">Guest last name (required for guest checkout).</param>
/// <param name="Email">Guest email (required for guest checkout).</param>
/// <param name="Password">Guest password (required for guest checkout).</param>
/// <param name="PhoneNumber">Guest phone number (optional).</param>
/// <param name="Currency">
/// ISO 4217 code the guest chose to be billed in; omitted means the base. Ignored for a
/// signed-in client who already has one.
/// </param>
public record PlaceOrderRequest(
    IReadOnlyList<PlaceOrderItemRequest> Items,
    string PaymentMethod,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? PhoneNumber,
    string? Currency = null);
