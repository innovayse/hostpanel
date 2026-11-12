/**
 * The versions of the wording a payer agrees to when a payment method is saved for later charges.
 *
 * @module types/consenttextversion
 */

/**
 * Which wording the payer ticked. The server keeps the value as evidence of what was shown, so it
 * is sent with every save session (the API binds it as `consentTextVersion`, at most 50 characters,
 * and refuses a session without it).
 *
 * A change to either wording needs a NEW value here: never edit the text under an existing value,
 * or old consents would point at words the payer never saw.
 */
export enum ConsentTextVersion {
  /** The consent line on the account's saved-methods card (`client.storedMethods.consent*`). */
  AccountCard = 'account-card-2026-11',
  /**
   * The agreement under the checkout box (`checkout.saveForRenewalsAgreement`), which describes saving the
   * method within the payment itself. The value changed with that wording; the earlier one
   * (`checkout-agreement-2026-11`) belongs to the two-step wording that no longer exists.
   */
  CheckoutAgreement = 'checkout-agreement-2026-11-b'
}
