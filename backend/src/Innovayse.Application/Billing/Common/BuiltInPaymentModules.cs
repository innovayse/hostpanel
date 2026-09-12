namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Module ids for the payment methods built into the checkout flow rather than loaded as
/// plugins. The client mirrors these exact literals in
/// <c>client/utils/paymentModules.ts</c> — the two must stay in lockstep.
/// </summary>
/// <remarks>
/// Lives in Application, not API: the availability query and the order handler both read
/// them, and neither may reference the API project.
/// </remarks>
public static class BuiltInPaymentModules
{
    /// <summary>Module id for the built-in Stripe card payment method.</summary>
    public const string Stripe = "stripe";

    /// <summary>Module id for the built-in manual bank transfer payment method.</summary>
    public const string BankTransfer = "bank_transfer";

    /// <summary>
    /// Integration slug the admin panel saves the Stripe on/off switch under — the literal
    /// the admin integrations list keys its card by.
    /// </summary>
    public const string StripeIntegrationSlug = "stripe";

    /// <summary>
    /// Integration slug the admin panel saves the bank transfer switch under. It differs from
    /// <see cref="BankTransfer"/>, the module id the checkout sends: the admin integrations
    /// page keys every integration by a hyphenated slug.
    /// </summary>
    public const string BankTransferIntegrationSlug = "bank-transfer";
}
