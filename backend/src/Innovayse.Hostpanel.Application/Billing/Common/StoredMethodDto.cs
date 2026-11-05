namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A payment method stored at a gateway module that the client has consented to have charged.</summary>
/// <param name="ConsentId">The consent that covers the method; the client revokes it by this id.</param>
/// <param name="GatewayModuleId">The payment plugin that holds the method.</param>
/// <param name="MethodRef">The gateway's reference for the method.</param>
/// <param name="Brand">The card brand, or <see langword="null"/> for a method that is not a card.</param>
/// <param name="Last4">The last four digits, or <see langword="null"/> for a method that is not a card.</param>
/// <param name="ExpiryMonth">The expiry month, 1 to 12.</param>
/// <param name="ExpiryYear">The four digit expiry year.</param>
/// <param name="IsDefault">Whether it is the default the host charges first for the client at this gateway; the host's own record.</param>
public sealed record StoredMethodDto(
    int ConsentId,
    string GatewayModuleId,
    string MethodRef,
    string? Brand,
    string? Last4,
    int ExpiryMonth,
    int ExpiryYear,
    bool IsDefault);
