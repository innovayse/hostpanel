namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A payment method a payment gateway module has on file for a client.</summary>
/// <param name="GatewayModuleId">The payment gateway module that holds the method (for example "stripe").</param>
/// <param name="Id">The gateway's reference for the method.</param>
/// <param name="Brand">Card network (e.g. "visa"), or <see langword="null"/> for a method that is not a card.</param>
/// <param name="Last4">The last four digits of the card, or <see langword="null"/> for a method that is not a card.</param>
/// <param name="ExpiryMonth">Card expiry month.</param>
/// <param name="ExpiryYear">Card expiry year (4-digit).</param>
/// <param name="IsDefault">Whether this is the customer's default payment method.</param>
/// <param name="AutoCharge">Whether the client has agreed that renewals may be charged to this method without them. Filled in from the agreements on file, never by the gateway.</param>
public sealed record CardPaymentMethodDto(
    string GatewayModuleId,
    string Id,
    string? Brand,
    string? Last4,
    int ExpiryMonth,
    int ExpiryYear,
    bool IsDefault,
    bool AutoCharge = false);
