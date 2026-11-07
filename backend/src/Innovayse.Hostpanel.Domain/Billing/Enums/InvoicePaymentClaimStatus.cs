namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// Where one <see cref="InvoicePaymentClaim"/> stands: a charge of an invoice against a stored payment method
/// that the host started and has not yet seen end. The first three values are unresolved: while a claim holds one,
/// the invoice is claimed and no second stored charge may start for it. A held claim blocks a new charge too.
/// </summary>
public enum InvoicePaymentClaimStatus
{
    /// <summary>The claim was written and the plugin was (or is being) asked to charge; no answer has been recorded.</summary>
    Open = 1,

    /// <summary>The payer must authenticate before the charge can complete; the claim holds the address to send them to.</summary>
    ActionRequired = 2,

    /// <summary>The gateway accepted the charge but has not settled it; the key lookup decides it later.</summary>
    Pending = 3,

    /// <summary>The money was taken and recorded on the invoice. Final.</summary>
    Settled = 4,

    /// <summary>The gateway refused the charge. Final: a new attempt gets a new claim and a new idempotency key.</summary>
    Declined = 5,

    /// <summary>The gateway confirmed it never charged under the claim's key. Final: a new attempt may start.</summary>
    Abandoned = 6,

    /// <summary>
    /// The gateway took money that the invoice could not record (a different amount or currency, or the invoice is no
    /// longer open for payment). Final for the claim, and held for an operator. It keeps blocking: the invoice is not
    /// charged again until the held money is settled by hand.
    /// </summary>
    Held = 7,
}
