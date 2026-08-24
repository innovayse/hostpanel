namespace Innovayse.SDK.Plugins;

/// <summary>Input for <see cref="IPaymentPlugin.CreatePaymentAsync"/>.</summary>
/// <param name="OrderNumber">Merchant-side order number, unique per attempt, gateway limits apply (Inecobank: ≤ 25 chars).</param>
/// <param name="AmountMinor">Amount in minor currency units (e.g. luma for AMD).</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
/// <param name="Description">Optional free-form description; providers sanitize per their rules.</param>
/// <param name="Language">Optional ISO 639-1 language for the payment page; provider default applies when null.</param>
public sealed record PaymentRequest(
    string OrderNumber,
    long AmountMinor,
    string ReturnUrl,
    string? Description,
    string? Language);
