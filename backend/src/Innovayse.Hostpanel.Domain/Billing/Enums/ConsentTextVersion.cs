namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// The published versions of the wording a payer agrees to when a payment method is saved for later charges. A
/// consent keeps the version the payer ticked as evidence of what was shown, so only a version the portal really
/// published can be recorded. A change to either wording needs a new member: the text under an existing one is never
/// edited, or old consents would point at words the payer never saw. The portal sends the wire value of each member
/// (<c>ConsentTextVersionExtensions.ToWire</c> in Application), which the consent stores as text.
/// </summary>
public enum ConsentTextVersion
{
    /// <summary>The consent line on the account's saved-methods card, where a method is saved on its own.</summary>
    AccountCard = 1,

    /// <summary>The agreement under the checkout box, which describes saving the method within the payment itself.</summary>
    CheckoutAgreement = 2,
}
