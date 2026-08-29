namespace Innovayse.Providers.Inecobank;

/// <summary>Input for <see cref="InecobankApiClient.RegisterOrderAsync"/>.</summary>
/// <param name="OrderNumber">Merchant order number, unique per attempt, ≤ 25 chars.</param>
/// <param name="AmountMinor">Amount in minor currency units (luma).</param>
/// <param name="Currency">ISO 4217 numeric currency code — always sent explicitly (gateway defaults to 643/RUB).</param>
/// <param name="ReturnUrl">Absolute URL the payer is redirected back to.</param>
/// <param name="Description">Optional description; sanitized to the gateway's rules before sending.</param>
/// <param name="Language">Optional ISO 639-1 payment page language.</param>
public sealed record InecobankRegisterRequest(
    string OrderNumber, long AmountMinor, string Currency,
    string ReturnUrl, string? Description, string? Language);
