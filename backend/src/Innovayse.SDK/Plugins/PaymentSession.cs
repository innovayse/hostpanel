namespace Innovayse.SDK.Plugins;

/// <summary>Result of a successful payment registration at the gateway.</summary>
/// <param name="GatewayOrderId">The gateway-side order id; store it — status and refund calls key off it.</param>
/// <param name="RedirectUrl">The hosted payment page URL to redirect the payer to.</param>
public sealed record PaymentSession(string GatewayOrderId, string RedirectUrl);
