namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One entry of the client's saved payment methods: an active charge consent, with the method's details read live
/// from the gateway module when it could answer.
/// </summary>
/// <remarks>
/// The gateway keeps the details, the host keeps only the consent. When the module is disabled, fails, or no longer
/// holds the method, <paramref name="DetailsAvailable"/> is <see langword="false"/> and the detail members carry no
/// values; the consent is still listed so the client can revoke it.
/// </remarks>
/// <param name="ConsentId">The consent that covers the method; the client revokes it, or makes it the default, by this id.</param>
/// <param name="GatewayModuleId">The payment plugin that holds the method.</param>
/// <param name="DetailsAvailable">Whether the gateway answered with this method's details.</param>
/// <param name="Brand">The card brand, or <see langword="null"/> when the method has none or the details are unavailable.</param>
/// <param name="Last4">The last four digits, or <see langword="null"/> when the method has none or the details are unavailable.</param>
/// <param name="ExpiryMonth">The expiry month, 1 to 12; 0 when the method does not expire or the details are unavailable.</param>
/// <param name="ExpiryYear">The four digit expiry year; 0 when the method does not expire or the details are unavailable.</param>
/// <param name="IsDefault">Whether it is the default the host charges first for the client at this gateway; the host's own record, reported even when the details are unavailable.</param>
public sealed record StoredPaymentMethodDto(
    int ConsentId,
    string GatewayModuleId,
    bool DetailsAvailable,
    string? Brand,
    string? Last4,
    int ExpiryMonth,
    int ExpiryYear,
    bool IsDefault);
