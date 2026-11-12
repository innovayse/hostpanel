namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One payment method offered at checkout.
/// </summary>
/// <param name="Module">Module id the checkout sends back as the order's payment method.</param>
/// <param name="DisplayName">Label the payer sees; serialized as <c>displayName</c>.</param>
/// <param name="SupportsStoredCredentials">
/// Whether the module can keep the payment method a payer pays with for later charges, so checkout may offer to save it;
/// serialized as <c>supportsStoredCredentials</c>. Always <see langword="false"/> for a built-in method.
/// </param>
public sealed record AvailablePaymentMethodDto(string Module, string DisplayName, bool SupportsStoredCredentials);
