namespace Innovayse.Application.Billing.Common;

/// <summary>A card or bank account Stripe has on file for a customer.</summary>
/// <param name="Id">The Stripe PaymentMethod ID (e.g. <c>pm_1AbC...</c>).</param>
/// <param name="Type">Stripe's payment method type (<c>card</c> or <c>us_bank_account</c>).</param>
/// <param name="Brand">Card network (e.g. "visa"), or the bank name for a bank account.</param>
/// <param name="Last4">The last four digits of the card or account number.</param>
/// <param name="ExpiryMonth">Card expiry month, or <see langword="null"/> for a bank account.</param>
/// <param name="ExpiryYear">Card expiry year (4-digit), or <see langword="null"/> for a bank account.</param>
/// <param name="IsDefault">Whether this is the customer's default payment method.</param>
public sealed record StripePaymentMethodDto(
    string Id,
    string Type,
    string? Brand,
    string? Last4,
    int? ExpiryMonth,
    int? ExpiryYear,
    bool IsDefault);
