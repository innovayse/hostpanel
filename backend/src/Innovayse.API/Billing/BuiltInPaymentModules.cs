namespace Innovayse.API.Billing;

/// <summary>
/// Module ids for the payment methods built into the checkout flow rather than loaded as
/// plugins. The client mirrors these exact literals in
/// <c>client/utils/paymentModules.ts</c> — the two must stay in lockstep.
/// </summary>
public static class BuiltInPaymentModules
{
    /// <summary>Module id for the built-in Stripe card payment method.</summary>
    public const string Stripe = "stripe";

    /// <summary>Module id for the built-in manual bank transfer payment method.</summary>
    public const string BankTransfer = "bank_transfer";
}
