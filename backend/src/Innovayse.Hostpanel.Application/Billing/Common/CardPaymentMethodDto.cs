using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A card or bank account the card gateway has on file for a customer.</summary>
/// <param name="Id">The card gateway payment method ID (a <c>pm_</c> prefixed id).</param>
/// <param name="Type">The kind of method, mapped from the gateway's own type code by its adapter.</param>
/// <param name="Brand">Card network (e.g. "visa"), or the bank name for a bank account.</param>
/// <param name="Last4">The last four digits of the card or account number.</param>
/// <param name="ExpiryMonth">Card expiry month, or <see langword="null"/> for a bank account.</param>
/// <param name="ExpiryYear">Card expiry year (4-digit), or <see langword="null"/> for a bank account.</param>
/// <param name="IsDefault">Whether this is the customer's default payment method.</param>
/// <param name="AutoCharge">Whether the client has agreed that renewals may be charged to this card without them. Filled in from the agreements on file, never by the gateway.</param>
public sealed record CardPaymentMethodDto(
    string Id,
    SavedPaymentMethodType Type,
    string? Brand,
    string? Last4,
    int? ExpiryMonth,
    int? ExpiryYear,
    bool IsDefault,
    bool AutoCharge = false);
